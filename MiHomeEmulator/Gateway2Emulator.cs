using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace MiHomeEmulator;

public sealed class Gateway2Emulator : IAsyncDisposable
{
    private static readonly byte[] DeveloperKeyIv =
        [0x17, 0x99, 0x6d, 0x09, 0x3d, 0x28, 0xdd, 0xb3, 0xba, 0x69, 0x5a, 0x2e, 0x6f, 0x58, 0x56, 0x2e];
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _receiveTask;
    private readonly ConcurrentDictionary<string, Gateway2Device> _devices = new(StringComparer.Ordinal);
    private readonly Channel<Gateway2Write> _writes =
        Channel.CreateUnbounded<Gateway2Write>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private IPEndPoint? _clientEndPoint;
    private string _token;
    private int _respondToRequests = 1;
    private int _disposed;

    public Gateway2Emulator(string sid, string token, string password, IEnumerable<Gateway2Device>? devices = null,
        string miioToken = "0123456789abcdef0123456789abcdef")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        ValidateAesText(token, nameof(token));
        ValidateAesText(password, nameof(password));
        Sid = sid;
        _token = token;
        Password = password;
        MiioToken = miioToken;
        foreach (var device in devices ?? [])
            _devices[device.Sid] = CloneDevice(device);

        Miio = new MiioEmulator(MiioToken)
        {
            Handler = request => request["method"]?.GetValue<string>() switch
            {
                "get_lumi_dpf_aes_key" => new JsonArray(Password),
                "set_lumi_dpf_aes_key" => new JsonArray("ok"),
                _ => MiioEmulator.DefaultResult(request),
            },
        };
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        EndPoint = (IPEndPoint)_udp.Client.LocalEndPoint!;
        _receiveTask = ReceiveAsync();
    }

    public string Sid { get; }
    public string Password { get; }
    public string MiioToken { get; }
    public MiioEmulator Miio { get; }
    public int MiioPort => Miio.Port;
    public IPEndPoint MiioEndPoint => Miio.EndPoint;
    public IPEndPoint EndPoint { get; }
    public int Port => EndPoint.Port;
    public string Token => Volatile.Read(ref _token);
    public IReadOnlyList<Gateway2Device> Devices =>
        _devices.Values.Select(CloneDevice).ToArray();
    public ChannelReader<Gateway2Write> ReceivedWrites => _writes.Reader;

    public bool RespondToRequests
    {
        get => Volatile.Read(ref _respondToRequests) != 0;
        set => Volatile.Write(ref _respondToRequests, value ? 1 : 0);
    }

    public async Task SendReportAsync(string sid, JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!_devices.TryGetValue(sid, out var device))
            throw new KeyNotFoundException($"Gateway 2 device '{sid}' is not registered.");
        var clone = CloneDevice(device) with { Data = (JsonObject)data.DeepClone() };
        _devices[sid] = clone;
        await SendMessageAsync(new JsonObject
        {
            ["cmd"] = "report",
            ["model"] = clone.Model,
            ["sid"] = clone.Sid,
            ["short_id"] = clone.ShortId,
            ["data"] = JsonSerializer.Serialize(clone.Data),
        });
    }

    public async Task SendHeartbeatAsync(string? token = null)
    {
        if (token is not null)
        {
            ValidateAesText(token, nameof(token));
            Volatile.Write(ref _token, token);
        }
        await SendMessageAsync(new JsonObject
        {
            ["cmd"] = "heartbeat",
            ["model"] = "gateway",
            ["sid"] = Sid,
            ["token"] = Token,
            ["data"] = "{}",
        });
    }

    public async Task SendRawAsync(string message, IPEndPoint? destination = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        var endpoint = destination ?? _clientEndPoint ??
            throw new InvalidOperationException("No Gateway 2 client has sent a packet yet.");
        var bytes = Encoding.UTF8.GetBytes(message);
        await _udp.SendAsync(bytes, endpoint, _lifetime.Token);
    }

    private async Task SendMessageAsync(JsonObject message) =>
        await SendRawAsync(message.ToJsonString());

    private async Task ReceiveAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var datagram = await _udp.ReceiveAsync(_lifetime.Token);
                _clientEndPoint = datagram.RemoteEndPoint;
                JsonObject? message;
                try { message = JsonNode.Parse(datagram.Buffer) as JsonObject; }
                catch (JsonException) { continue; }
                if (message is null) continue;
                if (!RespondToRequests) continue;

                switch (message["cmd"]?.GetValue<string>())
                {
                    case "get_id_list":
                        await SendIdListAsync(datagram.RemoteEndPoint);
                        break;
                    case "read":
                        await SendReadAsync(message, datagram.RemoteEndPoint);
                        break;
                    case "write":
                        await HandleWriteAsync(message, datagram.RemoteEndPoint);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
        catch (SocketException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task SendIdListAsync(IPEndPoint destination)
    {
        await SendRawAsync(new JsonObject
        {
            ["cmd"] = "get_id_list_ack",
            ["model"] = "gateway",
            ["sid"] = Sid,
            ["token"] = Token,
            ["data"] = JsonSerializer.Serialize(_devices.Keys.ToArray()),
        }.ToJsonString(), destination);
    }

    private async Task SendReadAsync(JsonObject request, IPEndPoint destination)
    {
        var sid = request["sid"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(sid)) return;
        var isGateway = sid == Sid;
        var device = isGateway ? null : _devices.GetValueOrDefault(sid);
        if (!isGateway && device is null) return;
        await SendRawAsync(new JsonObject
        {
            ["cmd"] = "read_ack",
            ["model"] = isGateway ? "gateway" : device!.Model,
            ["sid"] = sid,
            ["short_id"] = isGateway ? "0" : device!.ShortId,
            ["token"] = isGateway ? Token : null,
            ["data"] = JsonSerializer.Serialize(isGateway
                ? new JsonObject { ["rgb"] = 0, ["illumination"] = 300, ["proto_version"] = "1.1.2" }
                : device!.Data),
        }.ToJsonString(), destination);
    }

    private async Task HandleWriteAsync(JsonObject request, IPEndPoint destination)
    {
        var sid = request["sid"]?.GetValue<string>();
        var model = request["model"]?.GetValue<string>();
        var data = request["data"] as JsonObject;
        var key = data?["key"]?.GetValue<string>();
        var validKey = false;
        if (key is not null)
        {
            try
            {
                validKey = CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(key), BuildDeveloperKey(Token, Password));
            }
            catch (FormatException) { }
        }
        if (sid is not null && model is not null && data is not null)
            await _writes.Writer.WriteAsync(new Gateway2Write(sid, model, (JsonObject)data.DeepClone(), validKey), _lifetime.Token);
        var responseData = validKey
            ? new JsonObject()
            : new JsonObject { ["error"] = "Invalid key" };

        await SendRawAsync(new JsonObject
        {
            ["cmd"] = "write_ack",
            ["model"] = model,
            ["sid"] = sid,
            ["data"] = JsonSerializer.Serialize(responseData),
        }.ToJsonString(), destination);

        if (!validKey || sid is null || model is null || data is null ||
            !_devices.TryGetValue(sid, out var device))
            return;

        var updated = (JsonObject)device.Data.DeepClone();
        foreach (var (name, value) in data)
        {
            if (name != "key") updated[name] = value?.DeepClone();
        }
        var newDevice = device with { Data = updated };
        _devices[sid] = newDevice;
        await SendRawAsync(new JsonObject
        {
            ["cmd"] = "report",
            ["model"] = model,
            ["sid"] = sid,
            ["short_id"] = device.ShortId,
            ["data"] = JsonSerializer.Serialize(updated),
        }.ToJsonString(), destination);
    }

    private static byte[] BuildDeveloperKey(string token, string password)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.ASCII.GetBytes(password);
        aes.IV = DeveloperKeyIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(token), 0, Encoding.UTF8.GetByteCount(token));
    }

    private static Gateway2Device CloneDevice(Gateway2Device device) =>
        device with { Data = (JsonObject)device.Data.DeepClone() };

    private static void ValidateAesText(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != 16 || value.Any(character => character > 0x7f))
            throw new ArgumentException("The Gateway 2 token and developer password must each be 16 ASCII characters.", parameterName);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _udp.Dispose();
        try { await _receiveTask.ConfigureAwait(false); }
        finally
        {
            await Miio.DisposeAsync().ConfigureAwait(false);
            _writes.Writer.TryComplete();
            _lifetime.Dispose();
        }
    }
}
