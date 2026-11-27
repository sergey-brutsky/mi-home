using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeEmulator;

public sealed class MiioEmulator : IAsyncDisposable
{
    private const int HeaderLength = 32;
    private const int MaxPacketLength = ushort.MaxValue;
    private static readonly byte[] Magic = [0x21, 0x31];
    private readonly byte[] _token;
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _receiveTask;
    private readonly object _requestsLock = new();
    private readonly List<JsonObject> _receivedRequests = [];
    private readonly ConcurrentQueue<byte[]> _rawBeforeResponse = new();
    private int _dropNextRequests;
    private int _disposed;

    public MiioEmulator(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Length != 32)
            throw new ArgumentException("The miIO token must contain exactly 32 hexadecimal characters.", nameof(token));

        try { _token = Convert.FromHexString(token); }
        catch (FormatException exception)
        {
            throw new ArgumentException("The miIO token must contain exactly 32 hexadecimal characters.", nameof(token), exception);
        }
        _key = MD5.HashData(_token);
        _iv = MD5.HashData([.. _key, .. _token]);
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        EndPoint = (IPEndPoint)_udp.Client.LocalEndPoint!;
        _receiveTask = ReceiveAsync();
        Handler = DefaultHandler;
    }

    public IPEndPoint EndPoint { get; }
    public int Port => EndPoint.Port;
    public Func<JsonObject, JsonNode?> Handler { get; set; }

    internal static JsonNode DefaultResult(JsonObject request) => DefaultHandler(request)!;

    public int DropNextRequests
    {
        get => Volatile.Read(ref _dropNextRequests);
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref _dropNextRequests, value);
        }
    }

    public IReadOnlyList<JsonObject> ReceivedRequests
    {
        get
        {
            lock (_requestsLock)
                return _receivedRequests.Select(request => (JsonObject)request.DeepClone()).ToArray();
        }
    }

    public async Task SendRawAsync(byte[] packet, IPEndPoint? destination = null)
    {
        ArgumentNullException.ThrowIfNull(packet);
        await _udp.SendAsync(packet, destination ?? _clientEndPoint ?? throw new InvalidOperationException("No miIO client has sent a packet yet."));
    }

    public void SendBeforeNextResponse(params byte[][] packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        foreach (var packet in packets)
        {
            ArgumentNullException.ThrowIfNull(packet);
            _rawBeforeResponse.Enqueue((byte[])packet.Clone());
        }
    }

    private IPEndPoint? _clientEndPoint;

    private async Task ReceiveAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var datagram = await _udp.ReceiveAsync(_lifetime.Token);
                _clientEndPoint = datagram.RemoteEndPoint;
                if (datagram.Buffer.Length == HeaderLength && datagram.Buffer.AsSpan(0, 2).SequenceEqual(Magic))
                {
                    await _udp.SendAsync(BuildHelloResponse(), datagram.RemoteEndPoint, _lifetime.Token);
                    continue;
                }

                var request = TryDecodeRequest(datagram.Buffer);
                if (request is null) continue;

                lock (_requestsLock) _receivedRequests.Add((JsonObject)request.DeepClone());
                if (ConsumeDropRequest()) continue;

                byte[] response;
                try
                {
                    while (_rawBeforeResponse.TryDequeue(out var raw))
                        await _udp.SendAsync(raw, datagram.RemoteEndPoint, _lifetime.Token);
                    response = BuildResponse(datagram.Buffer, request, Handler(request));
                }
                catch (MiioEmulatorError error)
                {
                    response = BuildErrorResponse(datagram.Buffer, request, error);
                }

                await _udp.SendAsync(response, datagram.RemoteEndPoint, _lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
        catch (SocketException) when (_lifetime.IsCancellationRequested) { }
    }

    private static JsonNode? DefaultHandler(JsonObject request)
    {
        var method = request["method"]?.GetValue<string>();
        return method switch
        {
            "miIO.info" => new JsonObject
            {
                ["life"] = 1,
                ["miio_ver"] = "0.0.0",
                ["fw_ver"] = "1.0.0",
                ["mac"] = "00:00:00:00:00:00",
                ["hw_ver"] = "emulator",
                ["ap"] = new JsonObject
                {
                    ["ssid"] = "loopback",
                    ["bssid"] = "00:00:00:00:00:00",
                    ["rssi"] = 0,
                    ["freq"] = 0,
                },
                ["netif"] = new JsonObject
                {
                    ["localIp"] = "127.0.0.1",
                    ["mask"] = "255.0.0.0",
                    ["gw"] = "127.0.0.1",
                },
                ["model"] = "miio.emulator",
            },
            "get_lumi_dpf_aes_key" => new JsonArray("0123456789abcdef"),
            "get_device_prop" => new JsonObject { ["code"] = 0, ["result"] = new JsonArray() },
            _ => new JsonArray(),
        };
    }

    private bool ConsumeDropRequest()
    {
        while (true)
        {
            var remaining = Volatile.Read(ref _dropNextRequests);
            if (remaining == 0) return false;
            if (Interlocked.CompareExchange(ref _dropNextRequests, remaining - 1, remaining) == remaining)
                return true;
        }
    }

    private byte[] BuildHelloResponse()
    {
        var response = new byte[HeaderLength];
        Magic.CopyTo(response, 0);
        response[2] = 0;
        response[3] = HeaderLength;
        response[8] = 0x12;
        response[9] = 0x34;
        response[10] = 0x56;
        response[11] = 0x78;
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(12, 4), (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _token.CopyTo(response, 16);
        return response;
    }

    private JsonObject? TryDecodeRequest(byte[] packet)
    {
        if (packet.Length < HeaderLength || packet.Length > MaxPacketLength ||
            packet[0] != Magic[0] || packet[1] != Magic[1] ||
            BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2)) != packet.Length)
            return null;

        var encrypted = packet.AsSpan(HeaderLength).ToArray();
        var checksumInput = new byte[packet.Length];
        packet.AsSpan(0, 16).CopyTo(checksumInput);
        _token.CopyTo(checksumInput, 16);
        encrypted.CopyTo(checksumInput, HeaderLength);
        if (!CryptographicOperations.FixedTimeEquals(MD5.HashData(checksumInput), packet.AsSpan(16, 16)))
            return null;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            var clearText = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            var text = Encoding.UTF8.GetString(clearText).TrimEnd('\0');
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private byte[] BuildResponse(byte[] requestPacket, JsonObject request, JsonNode? result)
    {
        var response = new JsonObject { ["id"] = request["id"]?.DeepClone() };
        if (result is JsonObject resultObject &&
            (resultObject.ContainsKey("code") || resultObject.ContainsKey("error")))
        {
            foreach (var property in resultObject)
                response[property.Key] = property.Value?.DeepClone();
        }
        else
        {
            response["result"] = result?.DeepClone();
        }
        return BuildPacket(requestPacket, response);
    }

    private byte[] BuildErrorResponse(byte[] requestPacket, JsonObject request, MiioEmulatorError error) =>
        BuildPacket(requestPacket, new JsonObject
        {
            ["id"] = request["id"]?.DeepClone(),
            ["error"] = new JsonObject { ["code"] = error.Code, ["message"] = error.Message },
        });

    private byte[] BuildPacket(byte[] requestPacket, JsonObject response)
    {
        var text = Encoding.UTF8.GetBytes(response.ToJsonString());
        Array.Resize(ref text, text.Length + 1);
        var encrypted = Encrypt(text);
        var packet = new byte[HeaderLength + encrypted.Length];
        requestPacket.AsSpan(0, 16).CopyTo(packet);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _token.CopyTo(packet, 16);
        encrypted.CopyTo(packet, HeaderLength);
        MD5.HashData(packet).CopyTo(packet, 16);
        return packet;
    }

    private byte[] Encrypt(byte[] clearText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(clearText, 0, clearText.Length);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _udp.Dispose();
        try { await _receiveTask.ConfigureAwait(false); }
        finally { _lifetime.Dispose(); }
    }
}
