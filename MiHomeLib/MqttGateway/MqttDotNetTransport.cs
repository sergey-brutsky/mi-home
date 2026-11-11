using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;

namespace MiHomeLib.MqttGateway;

internal sealed class MqttDotNetTransport : IMqttTransport
{
    private const int TIMEOUT_MS = 5_000;
    private const ushort KEEP_ALIVE_SECONDS = 15;
    private static readonly Encoding Utf8 = Encoding.UTF8;

    private readonly ILogger _logger;
    private readonly TcpClient _client = new();
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _writeLock = new();
    private readonly string _commandsTopic;
    private readonly Task _receiveTask;
    private readonly Task _keepAliveTask;
    private volatile bool _disposed;
    private ushort _packetId;

    public MqttDotNetTransport(string ip, int port, string[] listenTopics, string commandsTopic, ILoggerFactory loggerFactory)
    {
        _commandsTopic = commandsTopic;
        _logger = loggerFactory.CreateLogger(GetType());

        try
        {
            WaitFor(_client.ConnectAsync(ip, port), "connect to the MQTT broker");
            _stream = _client.GetStream();
            _stream.WriteTimeout = TIMEOUT_MS;

            SendPacket(0x10, BuildConnectPayload());
            var connAck = WaitFor(ReadPacketAsync(CancellationToken.None), "receive the MQTT connection acknowledgement");
            if (connAck.Header != 0x20 || connAck.Body.Length != 2 ||
                connAck.Body[0] != 0 || connAck.Body[1] != 0)
            {
                var returnCode = connAck.Header == 0x20 && connAck.Body.Length == 2 ? connAck.Body[1] : -1;
                throw new IOException($"The MQTT broker rejected the connection (return code {returnCode}).");
            }

            var subscribePacketId = NextPacketId();
            SendPacket(0x82, BuildSubscribePayload(subscribePacketId, listenTopics));
            WaitForSubscriptionAck(subscribePacketId, listenTopics.Length);

            _logger.LogInformation("MQTT client connected to the broker --> {Ip}:{Port}", ip, port);
            _logger.LogInformation("MQTT client subscribed to the topics --> {Topics}", string.Join(",", listenTopics));
            _receiveTask = ReceiveLoopAsync(_cancellation.Token);
            _keepAliveTask = KeepAliveLoopAsync(_cancellation.Token);
        }
        catch
        {
            _client.Close();
            throw;
        }
    }

    public event Action<string, string> OnMessageReceived;

    public void SendMessage(string message) => SendCommand(_commandsTopic, message);

    public void SendCommand(string topic, string message)
    {
        ThrowIfDisposed();
        var body = new MemoryStream();
        WriteMqttString(body, topic);
        var payload = Utf8.GetBytes(message);
        body.Write(payload, 0, payload.Length);
        SendPacket(0x30, body.ToArray());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            SendPacket(0xE0, Array.Empty<byte>());
        }
        catch (IOException exception)
        {
            _logger.LogDebug(exception, "Could not send the MQTT disconnect packet.");
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception, "MQTT connection was already closed.");
        }
        _disposed = true;
        _cancellation.Cancel();
        _client.Close();
        _cancellation.Dispose();
    }

    private byte[] BuildConnectPayload()
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

    private void WaitForSubscriptionAck(ushort packetId, int topicCount)
    {
        MqttPacket response;
        do
        {
            response = WaitFor(ReadPacketAsync(CancellationToken.None), "receive the MQTT subscription acknowledgement");
            if ((response.Header >> 4) == 3)
                HandlePublish(response);
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

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var packet = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
                switch (packet.Header >> 4)
                {
                    case 3:
                        HandlePublish(packet);
                        break;
                    case 12:
                        SendPacket(0xD0, Array.Empty<byte>());
                        break;
                    case 14:
                        throw new IOException("The MQTT broker disconnected the client.");
                }
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (ObjectDisposedException) when (_disposed)
        {
        }
        catch (IOException exception) when (_disposed)
        {
            _logger.LogDebug(exception, "MQTT connection closed.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The MQTT receive loop stopped unexpectedly.");
        }
    }

    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(KEEP_ALIVE_SECONDS / 2), cancellationToken).ConfigureAwait(false);
                SendPacket(0xC0, Array.Empty<byte>());
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (ObjectDisposedException) when (_disposed)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The MQTT keep-alive loop stopped unexpectedly.");
        }
    }

    private void HandlePublish(MqttPacket packet)
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
        OnMessageReceived?.Invoke(topic, payload);
        if (qualityOfService == 1)
            SendPacket(0x40, new[] { (byte)(packetId >> 8), (byte)packetId });
    }

    private void SendPacket(byte header, byte[] body)
    {
        var packet = new MemoryStream();
        packet.WriteByte(header);
        WriteRemainingLength(packet, body.Length);
        packet.Write(body, 0, body.Length);
        var bytes = packet.ToArray();
        lock (_writeLock)
        {
            ThrowIfDisposed();
            _stream.Write(bytes, 0, bytes.Length);
        }
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
            remainingLength += (encodedLength & 0x7F) * multiplier;
            multiplier *= 128;
            if (++lengthBytes > 4)
                throw new InvalidDataException("The MQTT packet has an invalid remaining length.");
        } while ((encodedLength & 0x80) != 0);

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
        var buffer = new byte[1];
        var read = await _stream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
        if (read == 0)
            throw new EndOfStreamException("The MQTT broker closed the connection.");
        return buffer[0];
    }

    private ushort NextPacketId()
    {
        if (++_packetId == 0)
            ++_packetId;
        return _packetId;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MqttDotNetTransport));
    }

    private static T WaitFor<T>(Task<T> task, string operation)
    {
        if (!Task.WhenAny(task, Task.Delay(TIMEOUT_MS)).GetAwaiter().GetResult().Equals(task))
            throw new TimeoutException($"Timed out while attempting to {operation}.");
        return task.GetAwaiter().GetResult();
    }

    private static void WaitFor(Task task, string operation)
    {
        if (!Task.WhenAny(task, Task.Delay(TIMEOUT_MS)).GetAwaiter().GetResult().Equals(task))
            throw new TimeoutException($"Timed out while attempting to {operation}.");
        task.GetAwaiter().GetResult();
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
}
