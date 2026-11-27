using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Helpers;

namespace MiHomeLib.MqttGateway;

internal sealed class MqttTransport : IMqttTransport, IMqttConnectionEvents, IMqttCommandTransport
{
    private const int TIMEOUT_MS = 5_000;
    private const ushort KEEP_ALIVE_SECONDS = 15;
    private static readonly Encoding Utf8 = Encoding.UTF8;
    private readonly string _ip;
    private readonly int _port;
    private readonly string[] _listenTopics;
    private readonly string _commandsTopic;
    private readonly int _maxPacketBytes;
    private readonly TimeSpan _operationTimeout;
    private readonly TimeSpan _pingInterval;
    private readonly TimeSpan _pingResponseTimeout;
    private readonly ILogger _logger;
    private readonly object _writeLock = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private Connection _connection;
    private Task _connectionCompletion = Task.CompletedTask;
    private Task _reconnectTask = Task.CompletedTask;
    private int _disposed;
    private ushort _packetId;
    private readonly MqttCommandTracker _commands = new();
    private readonly object _deliveryLock = new();
    private readonly Queue<PublishedMessage> _deliveries = new();
    private readonly SemaphoreSlim _deliveryAvailable = new(0);
    private Task _deliveryTask = Task.CompletedTask;
    private int _pendingBytes;
    private const int MAX_PENDING_MESSAGES = 256;
    private const int MAX_PENDING_BYTES = 8 * 1024 * 1024;

    internal IReadOnlyList<TimeSpan> ReconnectDelays { get; set; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
         TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    internal Task Completion
    {
        get
        {
            lock (_writeLock) return Task.WhenAll(_connectionCompletion, _reconnectTask, _deliveryTask);
        }
    }

    public MqttTransport(string ip, int port, string[] listenTopics, string commandsTopic, ILoggerFactory loggerFactory,
        TimeSpan? operationTimeout = null, TimeSpan? pingInterval = null, TimeSpan? pingResponseTimeout = null, int maxPacketBytes = 1024 * 1024)
    {
        _ip = ip;
        _port = port;
        _listenTopics = (string[])listenTopics.Clone();
        _commandsTopic = commandsTopic;
        _maxPacketBytes = maxPacketBytes;
        _operationTimeout = operationTimeout ?? TimeSpan.FromMilliseconds(TIMEOUT_MS);
        _pingInterval = pingInterval ?? TimeSpan.FromSeconds(KEEP_ALIVE_SECONDS / 2);
        _pingResponseTimeout = pingResponseTimeout ?? TimeSpan.FromSeconds(KEEP_ALIVE_SECONDS);
        _logger = loggerFactory.CreateLogger(GetType());
        _lifetimeToken = _lifetime.Token;

        try
        {
            if (_maxPacketBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            if (_operationTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(operationTimeout), operationTimeout, "MQTT operation timeout must be positive.");
            if (_pingInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(pingInterval), pingInterval, "MQTT ping interval must be positive.");
            if (_pingResponseTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(pingResponseTimeout), pingResponseTimeout, "MQTT ping response timeout must be positive.");
            var connection = Connection.Open(this);
            _deliveryTask = Task.Run(DeliverMessagesAsync);
            lock (_writeLock)
            {
                _connection = connection;
                connection.Start();
                _connectionCompletion = connection.Completion;
            }
        }
        catch
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            throw;
        }
    }

    public event Action<string, string> OnMessageReceived;
    public event Action ConnectionLost;
    public event Action ConnectionRestored;

    public void SendMessage(string message) => Publish(_commandsTopic, message);

    public Task SendCommandAsync(string message) =>
        _commands.SendAsync(message, SendMessage, _operationTimeout, _lifetimeToken);

    public void Publish(string topic, string message)
    {
        ThrowIfDisposed();
        var body = new MemoryStream();
        WriteMqttString(body, topic);
        var payload = Utf8.GetBytes(message);
        body.Write(payload, 0, payload.Length);
        SendPacket(0x30, body.ToArray());
    }

    private void SendPacket(byte header, byte[] body)
    {
        lock (_writeLock)
        {
            ThrowIfDisposed();
            if (_connection is null)
                throw new InvalidOperationException($"MQTT connection to the gateway {_ip}:{_port} is lost, reconnecting.");
            _connection.SendPacket(header, body);
        }
    }

    public void Dispose()
    {
        Task completion;
        lock (_writeLock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            _connection?.Close(sendDisconnect: true);
            _connection = null;
            completion = Task.WhenAll(_connectionCompletion, _reconnectTask, _deliveryTask);
        }
        lock (_deliveryLock) { _deliveries.Clear(); _pendingBytes = 0; }
        _ = completion.ContinueWith(_ =>
        {
            _deliveryAvailable.Dispose();
            _lifetime.Dispose();
        }, TaskScheduler.Default);
    }

    private void OnConnectionFailed(Connection connection, Exception exception)
    {
        lock (_writeLock)
        {
            if (Volatile.Read(ref _disposed) != 0 || !ReferenceEquals(connection, _connection)) return;
            _connection = null;
            connection.Close();
        }
        _logger.LogWarning(exception, "MQTT connection to the gateway is lost, reconnecting");
        _commands.Disconnect();
        MiHomeMetrics.RecordMqttConnectionLost();
        RaiseConnectionEvent(ConnectionLost);
        lock (_writeLock)
        {
            if (Volatile.Read(ref _disposed) == 0)
                _reconnectTask = ReconnectAfterAsync(_reconnectTask);
        }
    }

    private async Task ReconnectAfterAsync(Task previous)
    {
        // A newly restored connection may fail before the preceding reconnect task returns.
        await previous.ConfigureAwait(false);
        await ReconnectLoopAsync().ConfigureAwait(false);
    }

    private async Task ReconnectLoopAsync()
    {
        var attempt = 0;
        while (!_lifetimeToken.IsCancellationRequested)
        {
            try
            {
                var delays = ReconnectDelays;
                await Task.Delay(delays[Math.Min(attempt, delays.Count - 1)], _lifetimeToken).ConfigureAwait(false);
                if (attempt < delays.Count - 1) attempt++;
                var connection = await Task.Run(() => Connection.Open(this), _lifetimeToken).ConfigureAwait(false);
                lock (_writeLock)
                {
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        connection.Close();
                        return;
                    }
                    _connection = connection;
                }
                _logger.LogInformation("MQTT connection to the gateway is restored");
                MiHomeMetrics.RecordMqttConnectionRestored();
                RaiseConnectionEvent(ConnectionRestored);
                lock (_writeLock)
                {
                    // Announce restoration before a new receive loop can report another loss.
                    if (ReferenceEquals(_connection, connection) && Volatile.Read(ref _disposed) == 0)
                    {
                        connection.Start();
                        _connectionCompletion = connection.Completion;
                    }
                }
                return;
            }
            catch (Exception) when (_lifetimeToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "MQTT reconnect attempt failed.");
            }
        }
    }

    private void RaiseConnectionEvent(Action handlers)
    {
        if (handlers is null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception exception) { _logger.LogError(exception, "Connection event subscriber failed."); }
        }
    }

    private ushort NextPacketId()
    {
        lock (_writeLock)
        {
            if (++_packetId == 0) ++_packetId;
            return _packetId;
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(MqttTransport));
    }

    private static byte[] BuildConnectPayload()
    {
        var body = new MemoryStream();
        WriteMqttString(body, "MQTT");
        body.WriteByte(4);
        body.WriteByte(0x02);
        WriteUInt16(body, KEEP_ALIVE_SECONDS);
        WriteMqttString(body, "MiHomeLib-" + Guid.NewGuid().ToString("N"));
        return body.ToArray();
    }

    private static byte[] BuildSubscribePayload(ushort packetId, string[] topics)
    {
        var body = new MemoryStream();
        WriteUInt16(body, packetId);
        foreach (var topic in topics)
        {
            WriteMqttString(body, topic);
            body.WriteByte(0);
        }
        return body.ToArray();
    }

    private void HandlePublish(Connection connection, MqttPacket packet)
    {
        var message = ParsePublish(packet);
        _commands.Receive(message.Payload);
        EnqueuePublish(message);
        connection.Acknowledge(message.PacketId);
    }

    private void EnqueuePublish(PublishedMessage message)
    {
        lock (_deliveryLock)
        {
            if (_lifetimeToken.IsCancellationRequested) return;
            var size = Utf8.GetByteCount(message.Topic) + Utf8.GetByteCount(message.Payload);
            // Disconnect on overload so the loss is visible to the client.
            if (_deliveries.Count >= MAX_PENDING_MESSAGES || size > MAX_PENDING_BYTES - _pendingBytes)
                throw new IOException("MQTT application delivery queue is full. A subscriber is processing messages too slowly.");
            _deliveries.Enqueue(message);
            _pendingBytes += size;
            _deliveryAvailable.Release();
        }
    }

    private async Task DeliverMessagesAsync()
    {
        try
        {
            while (true)
            {
                await _deliveryAvailable.WaitAsync(_lifetimeToken).ConfigureAwait(false);
                PublishedMessage message;
                lock (_deliveryLock)
                {
                    if (_lifetimeToken.IsCancellationRequested) return;
                    message = _deliveries.Dequeue();
                    _pendingBytes -= Utf8.GetByteCount(message.Topic) + Utf8.GetByteCount(message.Payload);
                }
                MessageReception.Run(message.ReceivedAt, () => DeliverPublish(message));
            }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
    }

    private static PublishedMessage ParsePublish(MqttPacket packet)
    {
        if (packet.Body.Length < 2)
            throw new InvalidDataException("The MQTT broker sent an invalid publish packet.");

        var topicLength = ReadUInt16(packet.Body, 0);
        if (packet.Body.Length < topicLength + 2)
            throw new InvalidDataException("The MQTT broker sent an invalid publish topic.");

        var topic = Utf8.GetString(packet.Body, 2, topicLength);
        var payloadOffset = topicLength + 2;
        var qualityOfService = (packet.Header >> 1) & 0x03;
        ushort packetId = 0;
        if (qualityOfService > 1)
            throw new InvalidDataException("The MQTT broker sent an unsupported publish quality of service.");
        if (qualityOfService == 1)
        {
            if (packet.Body.Length < payloadOffset + 2)
                throw new InvalidDataException("The MQTT broker sent an invalid publish packet identifier.");
            packetId = ReadUInt16(packet.Body, payloadOffset);
            payloadOffset += 2;
        }

        var payload = Utf8.GetString(packet.Body, payloadOffset, packet.Body.Length - payloadOffset);
        return new PublishedMessage(topic, payload, qualityOfService == 1 ? packetId : null);
    }

    private void DeliverPublish(PublishedMessage message)
    {
        var handlers = OnMessageReceived;
        if (handlers is not null)
        {
            foreach (Action<string, string> handler in handlers.GetInvocationList())
            {
                if (_lifetimeToken.IsCancellationRequested) return;
                try { handler(message.Topic, message.Payload); }
                catch (Exception exception) { _logger.LogError(exception, "MQTT subscriber failed."); }
            }
        }
    }

    private readonly struct PublishedMessage(string topic, string payload, ushort? packetId)
    {
        public DateTime ReceivedAt { get; } = DateTime.UtcNow;
        public string Topic { get; } = topic;
        public string Payload { get; } = payload;
        public ushort? PacketId { get; } = packetId;
    }

    private static void WriteMqttString(Stream stream, string value)
    {
        var bytes = Utf8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
            throw new ArgumentException("An MQTT string cannot exceed 65,535 UTF-8 bytes.", nameof(value));
        WriteUInt16(stream, (ushort)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

    private static void WriteRemainingLength(Stream stream, int length)
    {
        do
        {
            var encodedByte = (byte)(length % 128);
            length /= 128;
            if (length > 0)
                encodedByte |= 0x80;
            stream.WriteByte(encodedByte);
        } while (length > 0);
    }

    private sealed class MqttPacket(byte header, byte[] body)
    {
        public byte Header { get; } = header;
        public byte[] Body { get; } = body;
    }

    private sealed class Connection
    {
        private readonly MqttTransport _owner;
        private readonly TcpClient _client = new();
        private NetworkStream _stream;
        private readonly byte[] _readByteBuffer = new byte[1];
        private readonly Queue<PublishedMessage> _pendingPublishes = new();
        private int _pendingPublishBytes;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly CancellationToken _cancellationToken;
        private Task _receiveTask = Task.CompletedTask;
        private Task _keepAliveTask = Task.CompletedTask;
        private long _pingSent;
        private int _stopped;
        public Task Completion => Task.WhenAll(_receiveTask, _keepAliveTask);

        private Connection(MqttTransport owner)
        {
            _owner = owner;
            _cancellationToken = _cancellation.Token;
        }

        public static Connection Open(MqttTransport owner)
        {
            var connection = new Connection(owner);
            try
            {
                using (owner._lifetimeToken.Register(() => connection.Close()))
                {
                    GatewayConnection.ConnectAsync(connection._client, owner._ip, owner._port, "MQTT",
                        " (see the gateway page in the project wiki)", owner._lifetimeToken).GetAwaiter().GetResult();
                    connection._stream = connection._client.GetStream();
                    connection._stream.WriteTimeout = TIMEOUT_MS;
                    connection.SendPacket(0x10, BuildConnectPayload());
                    var connAck = connection.WaitFor(connection.ReadPacketAsync(connection._cancellationToken),
                        "receive the MQTT connection acknowledgement");
                    if (connAck.Header != 0x20 || connAck.Body.Length != 2 ||
                        connAck.Body[0] != 0 || connAck.Body[1] != 0)
                    {
                        var returnCode = connAck.Header == 0x20 && connAck.Body.Length == 2 ? connAck.Body[1] : -1;
                        throw new IOException($"The MQTT broker rejected the connection (return code {returnCode}).");
                    }

                    var subscribePacketId = owner.NextPacketId();
                    connection.SendPacket(0x82, BuildSubscribePayload(subscribePacketId, owner._listenTopics));
                    connection.WaitForSubscriptionAck(subscribePacketId, owner._listenTopics.Length);
                    owner._lifetimeToken.ThrowIfCancellationRequested();
                }
                owner._logger.LogInformation("MQTT client connected to the broker --> {Ip}:{Port}", owner._ip, owner._port);
                owner._logger.LogInformation("MQTT client subscribed to the topics --> {Topics}", string.Join(",", owner._listenTopics));
                return connection;
            }
            catch
            {
                connection.Close();
                throw;
            }
        }

        public void Start()
        {
            // Assignment is protected by the owner's write lock before failure callbacks can run.
            _receiveTask = Task.Run(ReceiveLoopAsync);
            _keepAliveTask = Task.Run(KeepAliveLoopAsync);
        }

        public void Close(bool sendDisconnect = false)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            try
            {
                if (sendDisconnect) _stream?.Write(new byte[] { 0xE0, 0 }, 0, 2);
            }
            catch (IOException exception)
            {
                _owner._logger.LogDebug(exception, "Could not send the MQTT disconnect packet.");
            }
            catch (ObjectDisposedException exception)
            {
                _owner._logger.LogDebug(exception, "MQTT connection was already closed.");
            }
            finally
            {
                _cancellation.Cancel();
                _client.Close();
                _ = Completion.ContinueWith(_ => _cancellation.Dispose(), TaskScheduler.Default);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                // Handshake publications are delivered only after this connection becomes current.
                while (_pendingPublishes.Count > 0 && !_cancellationToken.IsCancellationRequested)
                    _owner.EnqueuePublish(_pendingPublishes.Dequeue());
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var packet = await ReadPacketAsync(_cancellationToken).ConfigureAwait(false);
                    switch (packet.Header >> 4)
                    {
                        case 3:
                            _owner.HandlePublish(this, packet);
                            break;
                        case 12:
                            SendPacket(0xD0, Array.Empty<byte>());
                            break;
                        case 13:
                            Interlocked.Exchange(ref _pingSent, 0);
                            break;
                        case 14:
                            throw new IOException("The MQTT broker disconnected the client.");
                    }
                }
            }
            catch (Exception) when (_cancellationToken.IsCancellationRequested) { }
            catch (Exception exception) { _owner.OnConnectionFailed(this, exception); }
        }

        private async Task KeepAliveLoopAsync()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_owner._pingInterval, _cancellationToken).ConfigureAwait(false);
                    var sent = Interlocked.Read(ref _pingSent);
                    if (sent != 0)
                    {
                        if (DateTime.UtcNow.Ticks - sent > _owner._pingResponseTimeout.Ticks)
                            throw new TimeoutException("MQTT broker did not answer the keep-alive ping.");
                        continue;
                    }
                    Interlocked.Exchange(ref _pingSent, DateTime.UtcNow.Ticks);
                    SendPacket(0xC0, Array.Empty<byte>());
                }
            }
            catch (Exception) when (_cancellationToken.IsCancellationRequested) { }
            catch (Exception exception) { _owner.OnConnectionFailed(this, exception); }
        }

        private void WaitForSubscriptionAck(ushort packetId, int topicCount)
        {
            MqttPacket response;
            var deadline = DateTime.UtcNow + _owner._operationTimeout;
            do
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException("Timed out waiting for the MQTT subscription acknowledgement.");
                response = WaitFor(ReadPacketAsync(CancellationToken.None), "receive the MQTT subscription acknowledgement", remaining);
                if ((response.Header >> 4) == 3)
                {
                    var message = ParsePublish(response);
                    var size = Utf8.GetByteCount(message.Topic) + Utf8.GetByteCount(message.Payload);
                    if (_pendingPublishes.Count >= MAX_PENDING_MESSAGES || size > MAX_PENDING_BYTES - _pendingPublishBytes)
                        throw new IOException("MQTT subscription delivery queue is full.");
                    _pendingPublishes.Enqueue(message);
                    _pendingPublishBytes += size;
                    // The broker need not wait for application delivery to receive its QoS1 ack.
                    Acknowledge(message.PacketId);
                }
                else if ((response.Header >> 4) == 12)
                    SendPacket(0xD0, Array.Empty<byte>());
            } while ((response.Header >> 4) != 9);

            if ((response.Header >> 4) != 9 || response.Body.Length < 3 ||
                ReadUInt16(response.Body, 0) != packetId || response.Body.Length != topicCount + 2)
            {
                throw new IOException("The MQTT broker returned an unexpected subscription response.");
            }

            for (var i = 2; i < response.Body.Length; i++)
            {
                if (response.Body[i] == 0x80)
                    throw new IOException("The MQTT broker rejected a topic subscription.");
                if (response.Body[i] > 2)
                    throw new IOException("The MQTT broker returned an invalid subscription quality of service.");
            }
        }

        public void SendPacket(byte header, byte[] body)
        {
            var packet = new MemoryStream();
            packet.WriteByte(header);
            WriteRemainingLength(packet, body.Length);
            packet.Write(body, 0, body.Length);
            var bytes = packet.ToArray();
            lock (_owner._writeLock)
            {
                _owner.ThrowIfDisposed();
                _stream.Write(bytes, 0, bytes.Length);
            }
        }

        public void Acknowledge(ushort? packetId)
        {
            if (packetId is ushort id)
                SendPacket(0x40, new[] { (byte)(id >> 8), (byte)id });
        }

        private async Task<MqttPacket> ReadPacketAsync(CancellationToken cancellationToken)
        {
            var header = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var remainingLength = 0;
            var multiplier = 1;
            byte encodedLength;
            var lengthBytes = 0;
            do
            {
                encodedLength = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                lengthBytes++;
                if (lengthBytes == 4 && (encodedLength & 0x80) != 0)
                    throw new InvalidDataException("The MQTT packet has an invalid remaining length.");
                remainingLength += (encodedLength & 0x7F) * multiplier;
                multiplier *= 128;
            } while ((encodedLength & 0x80) != 0);

            if (remainingLength > _owner._maxPacketBytes)
                throw new InvalidDataException($"MQTT packet body exceeds the {_owner._maxPacketBytes}-byte limit.");
            var body = new byte[remainingLength];
            var offset = 0;
            while (offset < body.Length)
            {
                var read = await _stream.ReadAsync(body, offset, body.Length - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new EndOfStreamException("The MQTT broker closed the connection.");
                offset += read;
            }
            return new MqttPacket(header, body);
        }

        private async Task<byte> ReadByteAsync(CancellationToken cancellationToken)
        {
            var read = await _stream.ReadAsync(_readByteBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("The MQTT broker closed the connection.");
            return _readByteBuffer[0];
        }


        private T WaitFor<T>(Task<T> task, string operation, TimeSpan? timeout = null)
        {
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(_owner._lifetimeToken);
            var delay = Task.Delay(timeout ?? _owner._operationTimeout, delayCancellation.Token);
            if (Task.WhenAny(task, delay).GetAwaiter().GetResult() != task)
            {
                _ = task.ContinueWith(failed => failed.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _owner._lifetimeToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Timed out while attempting to {operation}.");
            }
            delayCancellation.Cancel();
            return task.GetAwaiter().GetResult();
        }
    }
}
