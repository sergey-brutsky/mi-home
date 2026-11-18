using MiHomeLib.XiaomiGateway2;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("MiHomeUnitTests")]
// Lets Moq mock internal interfaces such as IGateway2Transport in the tests
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace MiHomeLib.MiioDevices.Internal;

internal partial class MiioTransport : IMiioTransport, IMiioRequests
{
    private readonly string _ip;
    private readonly string _token;
    private readonly UdpClient _udpClient;
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private int _disposed;
    private readonly IPEndPoint _endpoint;
    private const int MESSAGES_TIMEOUT_MS = 5000;
    private const int HELLO_LENGTH = 32;
    // Added to the request id of every repeated attempt: some devices ignore a request whose id they have already seen
    private const int RETRY_ID_STEP = 1_000_000;
    private const string HELLO_REQUEST = "21310020ffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

    private MiioPacket initialPacket = null;
    // Ids of requests sent through RequestAsync. They start far from the small ids devices number their own messages with
    private int _requestId = 100_000;
    private readonly MiioCipher _cipher;
    // Device clock: the last stamp the device sent and when it was received. Requests carry this stamp plus the time passed since,
    // a stamp that stays at the hello value is rejected by some devices after a while
    private uint _deviceStamp;
    private readonly System.Diagnostics.Stopwatch _sinceDeviceStamp = new();
    // A receive that outlived the wait of an earlier attempt; it is reused, so its datagram is not lost
    private Task<UdpReceiveResult> _pendingReceive;
    public string Ip => _ip;
    public string Token => _token;

    /// <summary>
    /// How long to wait for hello and a command together, including repeated attempts. Starts after acquiring the request queue.
    /// </summary>
    internal TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to wait for the answer to one attempt before the request is sent again.
    /// UDP datagrams get lost, so a lost request is repeated instead of waiting the whole <see cref="ResponseTimeout"/>
    /// </summary>
    internal TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);

    public MiioTransport(string ip, string token, int port = 54321)
    {
        _lifetimeToken = _lifetime.Token;
        _ip = ip ?? throw new ArgumentNullException(nameof(ip), "IP of device must be provided");
        _token = token ?? throw new ArgumentNullException(nameof(token), "Token for device communication must be provided");
        _cipher = new MiioCipher(token);

        _endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _udpClient = new UdpClient();
    }

    private TimeoutException CreateTimeoutException(int attempts) =>
        new($"Device '{_endpoint.Address}' did not respond within {ResponseTimeout.TotalSeconds} seconds ({attempts} attempts). " +
            $"Make sure the device is powered on, reachable from this machine (the same network/subnet) " +
            $"and UDP port {_endpoint.Port} is not blocked by a firewall or VPN.");

    public static List<(string ip, string type, string serial, string token)> SendDiscoverMessage(int port = 54321) =>
        SendDiscoverMessage(new IPEndPoint(IPAddress.Broadcast, port), MESSAGES_TIMEOUT_MS);

    internal static List<(string ip, string type, string serial, string token)> SendDiscoverMessage(IPEndPoint destination, int receiveTimeoutMs) =>
        SendDiscoverMessageAsync(destination, TimeSpan.FromMilliseconds(receiveTimeoutMs)).GetAwaiter().GetResult();

    internal static async Task<List<(string ip, string type, string serial, string token)>> SendDiscoverMessageAsync(
        IPEndPoint destination, TimeSpan receiveTimeout)
    {
        if (receiveTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(receiveTimeout));
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        socket.EnableBroadcast = true;
        var hello = HELLO_REQUEST.ToByteArray();
        await socket.SendAsync(hello, hello.Length, destination).ConfigureAwait(false);
        using var timeoutCancellation = new CancellationTokenSource();
        var deadline = Task.Delay(receiveTimeout, timeoutCancellation.Token);
        var devices = new List<(string ip, string type, string serial, string token)>();
        var seen = new HashSet<(string, string, string, string)>();
        try
        {
            while (true)
            {
                var receive = socket.ReceiveAsync();
                if (await Task.WhenAny(receive, deadline).ConfigureAwait(false) != receive)
                {
                    // Closing the socket releases the outstanding receive on every target framework.
                    socket.Close();
                    try { await receive.ConfigureAwait(false); }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                    break;
                }
                var received = await receive.ConfigureAwait(false);
                try
                {
                    var packet = new MiioPacket(received.Buffer);
                    var device = (received.RemoteEndPoint.Address.ToString(), packet.GetDeviceType(), packet.GetSerial(), packet.GetChecksum());
                    if (seen.Add(device)) devices.Add(device);
                }
                catch (InvalidDataException) { }
                if (deadline.IsCompleted) break;
            }
        }
        catch (SocketException) { }
        finally { timeoutCancellation.Cancel(); }
        return devices;
    }

    // Both sync and async callers use the same receive, so a datagram never goes to a receive nobody waits for.
    // The awaits do not capture a context, so blocking on them is safe
    public string SendMessage(string msg) => SendMessageAsync(msg).GetAwaiter().GetResult();

    public Task<string> SendMessageAsync(string msg)
    {
        var request = JsonNode.Parse(msg) as JsonObject;
        var requestId = request?["id"] is JsonValue idValue && idValue.TryGetValue<int>(out var id) ? id : (int?)null;
        return SendRequestAsync(msg, request, requestId, null, MiioRetry.ForMethod(
            request?["method"] is JsonValue methodValue && methodValue.TryGetValue<string>(out var method) ? method : null));
    }

    public Task<string> RequestAsync(string method, object parameters, MiioRetryPolicy retryPolicy)
    {
        var id = Interlocked.Increment(ref _requestId);
        var request = new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = JsonSerializer.SerializeToNode(parameters, MiioJson.Options),
        };
        return SendRequestAsync(request.ToJsonString(), request, id, null, retryPolicy);
    }

    private async Task<string> SendRequestAsync(string msg, JsonObject request, int? requestId, int? attemptLimit, MiioRetryPolicy retryPolicy)
    {
        await _requests.WaitAsync(_lifetimeToken).ConfigureAwait(false);
        try
        {
            _lifetimeToken.ThrowIfCancellationRequested();
            return await ExchangeAsync(msg, request, requestId, attemptLimit, retryPolicy).ConfigureAwait(false);
        }
        catch (Exception) when (_lifetimeToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(_lifetimeToken);
        }
        catch (TimeoutException)
        {
            initialPacket = null;
            _deviceStamp = 0;
            _sinceDeviceStamp.Reset();
            throw;
        }
        finally { _requests.Release(); }
    }

    private async Task<string> ExchangeAsync(string msg, JsonObject request, int? requestId, int? attemptLimit, MiioRetryPolicy retryPolicy)
    {
        var deadline = DateTime.UtcNow + ResponseTimeout;
        await SendHelloPacketIfNeededAsync(deadline).ConfigureAwait(false);

        var repeat = retryPolicy == MiioRetryPolicy.SafeToRepeat;
        if (!repeat) attemptLimit = 1;
        var sentIds = new HashSet<int>();
        var attempts = 0;

        while (DateTime.UtcNow < deadline && (!attemptLimit.HasValue || attempts < attemptLimit.Value))
        {
            var text = msg;
            if (requestId is not null)
            {
                var attemptId = unchecked(requestId.Value + attempts * RETRY_ID_STEP);
                sentIds.Add(attemptId);
                // The first attempt is sent as it is, only a repeated one gets a new id
                if (attempts > 0)
                {
                    request["id"] = attemptId;
                    text = request.ToJsonString();
                }
            }

            attempts++;
            var bytes = initialPacket.BuildMessage(text, _cipher, CurrentDeviceStamp());
            await _udpClient.SendAsync(bytes, bytes.Length, _endpoint).ConfigureAwait(false);

            var attemptEnd = repeat ? Min(DateTime.UtcNow + AttemptTimeout, deadline) : deadline;
            while (await ReceiveUntilAsync(attemptEnd).ConfigureAwait(false) is { } received)
            {
                if (received.Length <= HELLO_LENGTH) continue; // a late answer to the hello

                var packet = new MiioPacket(received);
                var response = packet.GetResponseData(_cipher);
                if (!TryReadResponseId(response, out var responseId)) continue;
                // A numbered request requires an actual matching id, including ids of its earlier attempts.
                // Raw requests deliberately sent without an id retain compatibility with unnumbered object responses.
                if (requestId.HasValue && (!responseId.HasValue || !sentIds.Contains(responseId.Value))) continue;
                RememberDeviceStamp(packet.Stamp);
                return response;
            }
        }

        if (!repeat && attempts > 0)
            throw new TimeoutException(CreateTimeoutException(attempts).Message +
                " The command was sent once; its outcome is unknown. Check device state before sending it again.");
        throw CreateTimeoutException(attempts);
    }

    private void RememberDeviceStamp(uint stamp)
    {
        _deviceStamp = stamp;
        _sinceDeviceStamp.Restart();
    }

    private uint CurrentDeviceStamp() => unchecked(_deviceStamp + (uint)_sinceDeviceStamp.Elapsed.TotalSeconds);

    // A missing id is distinct from an invalid id or malformed response. Only callers sending
    // an unnumbered raw request may accept a valid object with no id.
    private static bool TryReadResponseId(string response, out int? id)
    {
        id = null;
        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var hasId = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name != "id") continue;
                if (hasId || property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var value))
                    return false;
                hasId = true;
                id = value;
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    /// <summary>
    /// Returns the next datagram, or null if none arrives until the given time
    /// </summary>
    private async Task<byte[]> ReceiveUntilAsync(DateTime until)
    {
        while (true)
        {
            var remaining = until - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return null;

            // UdpClient.ReceiveAsync ignores Socket.ReceiveTimeout and netstandard2.0 has no cancellable overload,
            // so the receive is raced against a delay and, when the delay wins, kept for the next wait
            var receive = _pendingReceive ??= _udpClient.ReceiveAsync();

            using (var delayCancellation = new CancellationTokenSource())
            {
                var completed = await Task.WhenAny(receive, Task.Delay(remaining, delayCancellation.Token)).ConfigureAwait(false);
                delayCancellation.Cancel();
                if (completed != receive) return null;
            }

            _pendingReceive = null;
            try
            {
                var datagram = await receive.ConfigureAwait(false);
                if (datagram.RemoteEndPoint.Equals(_endpoint)) return datagram.Buffer;
            }
            catch (SocketException)
            {
                // e.g. an ICMP "port unreachable" for an earlier datagram, it is the same as no answer
                continue;
            }
        }
    }

    public string SendMessageRepeated(string msg, int times)
    {
        if (times < 1) throw new ArgumentOutOfRangeException(nameof(times));
        var request = JsonNode.Parse(msg) as JsonObject;
        var requestId = request?["id"] is JsonValue idValue && idValue.TryGetValue<int>(out var id) ? id : (int?)null;
        return SendRequestAsync(msg, request, requestId, times, MiioRetryPolicy.SafeToRepeat).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _lifetime.Dispose();
        // Closing the socket faults a pending receive, its exception is observed here
        _pendingReceive?.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        _udpClient?.Close();
    }

    private async Task SendHelloPacketIfNeededAsync(DateTime deadline)
    {
        if (initialPacket != null) return;

        var bytes = HELLO_REQUEST.ToByteArray();
        var attempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempts++;
            await _udpClient.SendAsync(bytes, bytes.Length, _endpoint).ConfigureAwait(false);

            var attemptEnd = Min(DateTime.UtcNow + AttemptTimeout, deadline);
            while (await ReceiveUntilAsync(attemptEnd).ConfigureAwait(false) is { } received)
            {
                // hello response message must be 32 bytes, anything else is a late answer to an earlier request
                if (received.Length != HELLO_LENGTH) continue;

                initialPacket = new MiioPacket(received);
                RememberDeviceStamp(initialPacket.Stamp);
                return;
            }
        }

        throw CreateTimeoutException(attempts);
    }
}
