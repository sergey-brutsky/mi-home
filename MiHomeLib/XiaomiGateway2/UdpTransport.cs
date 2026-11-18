using Microsoft.Extensions.Logging;
using MiHomeLib.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Text.Json.Nodes;

namespace MiHomeLib.XiaomiGateway2;

internal sealed class UdpTransport : IGateway2Transport
{
    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _destination;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly Task _receiveTask;
    private int _disposed;
    public string Token { get; set; }
    internal Task Completion => _receiveTask;
    internal TimeSpan ReceiveErrorDelay { get; set; } = TimeSpan.FromSeconds(1);
    internal Func<Task<UdpReceiveResult>> Receive { get; set; }

    public UdpTransport(string multicastAddress = "224.0.0.50", int serverPort = 9898,
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory = null)
        : this(CreateSocket(multicastAddress, serverPort), new IPEndPoint(IPAddress.Parse(multicastAddress), serverPort), loggerFactory) { }

    internal UdpTransport(UdpClient socket, IPEndPoint destination, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        _udpClient = socket;
        _destination = destination;
        _logger = (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateLogger(nameof(UdpTransport));
        Receive = () => _udpClient.ReceiveAsync();
        _receiveTask = ReceiveAsync();
    }

    private static UdpClient CreateSocket(string address, int port)
    {
        var socket = new UdpClient();
        try
        {
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            socket.JoinMulticastGroup(IPAddress.Parse(address));
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    private async Task ReceiveAsync()
    {
        try
        {
            var consecutiveErrors = 0;
            while (Volatile.Read(ref _disposed) == 0)
            {
                UdpReceiveResult packet;
                try
                {
                    packet = await Receive().ConfigureAwait(false);
                    consecutiveErrors = 0;
                }
                catch (SocketException exception)
                {
                    if (Volatile.Read(ref _disposed) != 0) break;
                    if (++consecutiveErrors >= 10) throw;
                    _logger.LogWarning(exception, "Gateway 2 UDP receive failed, receiving continues");
                    await Task.Delay(ReceiveErrorDelay).ConfigureAwait(false);
                    continue;
                }
                var message = Encoding.UTF8.GetString(packet.Buffer);
                var handlers = OnMessageReceived;
                if (handlers is null) continue;
                foreach (Action<string> handler in handlers.GetInvocationList())
                {
                    try { handler(message); }
                    catch (Exception exception) { _logger.LogError(exception, "Gateway 2 UDP subscriber failed."); }
                }
            }
        }
        catch (Exception) when (Volatile.Read(ref _disposed) != 0) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Gateway 2 UDP receive loop stopped.");
            Dispose();
        }
    }

    public void SendDiscover() => Send(DiscoverMessage());

    public void SendRead(string sid) => Send(ReadMessage(sid));

    public void SendWrite(string sid, string model, string gatewayPassword, params (string Key, string Value)[] data) =>
        Send(WriteMessage(sid, model, Gateway2KeyBuilder.BuildKey(Token, gatewayPassword).ToHex(), data));

    internal static JsonObject DiscoverMessage() => new() { ["cmd"] = "get_id_list" };

    internal static JsonObject ReadMessage(string sid) => new() { ["cmd"] = "read", ["sid"] = sid };

    internal static JsonObject WriteMessage(string sid, string model, string key, params (string Key, string Value)[] data)
    {
        var values = new JsonObject();
        foreach (var (name, value) in data) values[name] = value;
        values["key"] = key;

        return new JsonObject { ["cmd"] = "write", ["model"] = model, ["sid"] = sid, ["data"] = values };
    }

    private void Send(JsonObject message)
    {
        var buffer = Encoding.UTF8.GetBytes(message.ToJsonString());
        _udpClient.Send(buffer, buffer.Length, _destination);
    }

    public event Action<string> OnMessageReceived;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _udpClient.Dispose();
    }
}
