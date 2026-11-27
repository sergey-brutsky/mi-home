using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace MiHomeEmulator;

public sealed class MqttBrokerEmulator : IAsyncDisposable
{
    private const int MaxIncomingPacketBytes = 8 * 1024 * 1024;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _acceptTask;
    private readonly ConcurrentDictionary<long, ClientSession> _clients = new();
    private readonly Channel<(string Topic, string Payload)> _received =
        Channel.CreateUnbounded<(string Topic, string Payload)>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private long _nextClientId;
    private int _nextPacketId;
    private int _refuseConnections;
    private int _disposed;

    public MqttBrokerEmulator()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        EndPoint = (IPEndPoint)_listener.LocalEndpoint;
        _acceptTask = AcceptAsync();
    }

    public IPEndPoint EndPoint { get; }
    public int Port => EndPoint.Port;
    public ChannelReader<(string Topic, string Payload)> Received => _received.Reader;
    public int ConnectedClientCount => _clients.Count;

    public bool RefuseConnections
    {
        get => Volatile.Read(ref _refuseConnections) != 0;
        set => Volatile.Write(ref _refuseConnections, value ? 1 : 0);
    }

    public async Task PublishAsync(string topic, string payload, int qos = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(payload);
        if (qos is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(qos), "Only MQTT QoS 0 and QoS 1 are supported.");
        var packet = BuildPublishPacket(topic, payload, qos, qos == 1 ? NextPacketId() : (ushort)0);
        foreach (var client in _clients.Values)
        {
            if (client.IsConnected && client.IsSubscribed(topic))
                await client.SendPacketAsync(packet.Header, packet.Body, _lifetime.Token).ConfigureAwait(false);
        }
    }

    public async Task SendRawAsync(byte[] packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        foreach (var client in _clients.Values)
        {
            if (client.IsConnected)
                await client.SendRawAsync(packet, _lifetime.Token).ConfigureAwait(false);
        }
    }

    public async Task DropConnectionsAsync()
    {
        var clients = _clients.Values.ToArray();
        foreach (var client in clients) client.Close();
        await Task.WhenAll(clients.Select(client => client.Completion)).ConfigureAwait(false);
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false);
                tcpClient.NoDelay = true;
                var id = Interlocked.Increment(ref _nextClientId);
                var session = new ClientSession(this, id, tcpClient);
                _clients[id] = session;
                session.Start();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
        catch (SocketException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task HandleClientAsync(ClientSession client)
    {
        try
        {
            while (!_lifetime.IsCancellationRequested && client.IsConnected)
            {
                var packet = await ReadPacketAsync(client.Stream, _lifetime.Token).ConfigureAwait(false);
                if (packet is null) break;
                var type = packet.Value.Header >> 4;
                switch (type)
                {
                    case 1:
                        if (RefuseConnections)
                        {
                            await client.SendPacketAsync(0x20, [0, 5], _lifetime.Token).ConfigureAwait(false);
                            return;
                        }
                        await client.SendPacketAsync(0x20, [0, 0], _lifetime.Token).ConfigureAwait(false);
                        break;
                    case 8:
                        await HandleSubscribeAsync(client, packet.Value.Body).ConfigureAwait(false);
                        break;
                    case 3:
                        await HandlePublishAsync(client, packet.Value.Header, packet.Value.Body).ConfigureAwait(false);
                        break;
                    case 12:
                        await client.SendPacketAsync(0xd0, [], _lifetime.Token).ConfigureAwait(false);
                        break;
                    case 14:
                        return;
                    case 4:
                    case 13:
                        break;
                    default:
                        return;
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or InvalidDataException)
        {
        }
        finally
        {
            client.Close();
            _clients.TryRemove(client.Id, out _);
        }
    }

    private async Task HandleSubscribeAsync(ClientSession client, byte[] body)
    {
        if (body.Length < 5) throw new InvalidDataException("Malformed MQTT SUBSCRIBE packet.");
        var packetId = BinaryPrimitives.ReadUInt16BigEndian(body);
        var offset = 2;
        var granted = new List<byte>();
        while (offset < body.Length)
        {
            var topic = ReadMqttString(body, ref offset);
            if (offset >= body.Length) throw new InvalidDataException("Malformed MQTT subscription.");
            var qos = body[offset++];
            if (qos > 1) throw new InvalidDataException("Unsupported MQTT subscription QoS.");
            client.Subscribe(topic);
            granted.Add(0);
        }

        var response = new byte[2 + granted.Count];
        BinaryPrimitives.WriteUInt16BigEndian(response, packetId);
        granted.CopyTo(response, 2);
        await client.SendPacketAsync(0x90, response, _lifetime.Token).ConfigureAwait(false);
    }

    private async Task HandlePublishAsync(ClientSession client, byte header, byte[] body)
    {
        var qos = (header >> 1) & 0x03;
        if (qos > 1) throw new InvalidDataException("Unsupported MQTT PUBLISH QoS.");
        var offset = 0;
        var topic = ReadMqttString(body, ref offset);
        ushort packetId = 0;
        if (qos == 1)
        {
            if (offset + 2 > body.Length) throw new InvalidDataException("Malformed MQTT PUBLISH packet.");
            packetId = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(offset, 2));
            offset += 2;
        }
        var payload = Encoding.UTF8.GetString(body, offset, body.Length - offset);
        await _received.Writer.WriteAsync((topic, payload), _lifetime.Token).ConfigureAwait(false);
        if (qos == 1)
        {
            var response = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(response, packetId);
            await client.SendPacketAsync(0x40, response, _lifetime.Token).ConfigureAwait(false);
        }
    }

    private ushort NextPacketId()
    {
        var id = Interlocked.Increment(ref _nextPacketId);
        if (id > ushort.MaxValue)
        {
            Interlocked.Exchange(ref _nextPacketId, 1);
            id = 1;
        }
        return (ushort)id;
    }

    private static (byte Header, byte[] Body) BuildPublishPacket(string topic, string payload, int qos, ushort packetId)
    {
        using var body = new MemoryStream();
        WriteMqttString(body, topic);
        if (qos == 1)
        {
            body.WriteByte((byte)(packetId >> 8));
            body.WriteByte((byte)packetId);
        }
        var data = Encoding.UTF8.GetBytes(payload);
        body.Write(data, 0, data.Length);
        return ((byte)(0x30 | (qos << 1)), body.ToArray());
    }

    private static async Task<(byte Header, byte[] Body)?> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        var first = new byte[1];
        var read = await stream.ReadAsync(first, cancellationToken).ConfigureAwait(false);
        if (read == 0) return null;
        var header = first[0];

        var remainingLength = 0;
        var multiplier = 1;
        var digits = 0;
        while (true)
        {
            await ReadExactlyAsync(stream, first, cancellationToken).ConfigureAwait(false);
            var digit = first[0];
            remainingLength = checked(remainingLength + (digit & 0x7f) * multiplier);
            digits++;
            if ((digit & 0x80) == 0) break;
            if (digits == 4) throw new InvalidDataException("Invalid MQTT remaining length.");
            multiplier = checked(multiplier * 128);
        }
        if (remainingLength > MaxIncomingPacketBytes) throw new InvalidDataException("MQTT packet exceeds emulator limit.");
        var body = new byte[remainingLength];
        await ReadExactlyAsync(stream, body, cancellationToken).ConfigureAwait(false);
        return (header, body);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static string ReadMqttString(byte[] body, ref int offset)
    {
        if (offset + 2 > body.Length) throw new InvalidDataException("Malformed MQTT string.");
        var length = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(offset, 2));
        offset += 2;
        if (offset + length > body.Length) throw new InvalidDataException("Malformed MQTT string length.");
        var value = Encoding.UTF8.GetString(body, offset, length);
        offset += length;
        return value;
    }

    private static void WriteMqttString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
        stream.WriteByte((byte)(bytes.Length >> 8));
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private void OnClientClosed(long id) => _clients.TryRemove(id, out _);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _listener.Stop();
        try
        {
            await _acceptTask.ConfigureAwait(false);
            var clients = _clients.Values.ToArray();
            foreach (var client in clients) client.Close();
            await Task.WhenAll(clients.Select(client => client.Completion)).ConfigureAwait(false);
        }
        finally
        {
            _received.Writer.TryComplete();
            _lifetime.Dispose();
        }
    }

    private sealed class ClientSession(MqttBrokerEmulator owner, long id, TcpClient tcpClient)
    {
        private readonly ConcurrentDictionary<string, byte> _subscriptions = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private int _closed;

        public long Id { get; } = id;
        public NetworkStream Stream { get; } = tcpClient.GetStream();
        public bool IsConnected => Volatile.Read(ref _closed) == 0 && tcpClient.Connected;
        public Task Completion { get; private set; } = Task.CompletedTask;

        public void Start() => Completion = owner.HandleClientAsync(this);

        public void Subscribe(string topic) => _subscriptions[topic] = 0;

        public bool IsSubscribed(string topic) => _subscriptions.ContainsKey(topic);

        public async Task SendPacketAsync(byte header, byte[] body, CancellationToken cancellationToken)
        {
            var remainingBytes = EncodeRemainingLength(body.Length);
            var packet = new byte[1 + remainingBytes.Length + body.Length];
            packet[0] = header;
            remainingBytes.CopyTo(packet, 1);
            body.CopyTo(packet, 1 + remainingBytes.Length);
            await SendRawAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendRawAsync(byte[] packet, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsConnected)
                    await Stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            }
            finally { _writeLock.Release(); }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { tcpClient.Close(); }
            finally { owner.OnClientClosed(Id); }
        }

        private static byte[] EncodeRemainingLength(int length)
        {
            var result = new List<byte>(4);
            do
            {
                var digit = (byte)(length % 128);
                length /= 128;
                if (length > 0) digit |= 0x80;
                result.Add(digit);
            } while (length > 0);
            return result.ToArray();
        }
    }
}
