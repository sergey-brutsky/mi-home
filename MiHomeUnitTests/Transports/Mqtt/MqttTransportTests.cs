using MiHomeUnitTests.Infrastructure;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests.Transports.Mqtt;

[Xunit.Trait("Category", "Integration")]
public class MqttTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publications_During_Resubscription_Are_Delivered_Only_After_A_Successful_Handshake(bool rejectHandshake)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var logs = new ErrorRecordingLoggerFactory();
        var creation = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            ["test"], "write", logs));
        using var broker = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await CompleteHandshake(broker, 1);
        using var transport = await creation.WaitAsync(TimeSpan.FromSeconds(3));
        transport.ReconnectDelays = [TimeSpan.FromMilliseconds(20)];
        var restored = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messages = new System.Collections.Generic.List<string>();
        transport.ConnectionRestored += () => restored.TrySetResult(true);
        transport.OnMessageReceived += (_, message) =>
        {
            messages.Add(message);
            transport.Publish("test", "reply-" + message);
            if (messages.Count == (rejectHandshake ? 1 : 2)) received.TrySetResult(true);
        };
        broker.Close();
        using var reconnecting = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        var stream = reconnecting.GetStream();
        await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 });
        var subscribe = await ReadPacket(stream);
        for (byte id = 1; id <= 2; id++)
        {
            await stream.WriteAsync(new byte[] { 0x32, 6, 0, 1, (byte)'t', 0, id, (byte)('a' + id - 1) });
            // QoS1 must be acknowledged even while application delivery is deferred.
            (await ReadPacket(stream)).Should().Equal(0, id);
        }
        messages.Should().BeEmpty();
        await stream.WriteAsync(new byte[] { 0x90, 3, subscribe[0], subscribe[1], rejectHandshake ? (byte)128 : (byte)0 });
        if (rejectHandshake)
        {
            using var retry = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await CompleteHandshake(retry, 1);
            await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
            messages.Should().BeEmpty();
            await retry.GetStream().WriteAsync(new byte[] { 0x30, 4, 0, 1, (byte)'t', (byte)'c' });
            await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            messages.Should().Equal("c");
            System.Text.Encoding.UTF8.GetString(await ReadPacket(retry.GetStream())).Should().EndWith("testreply-c");
            transport.Dispose();
            await transport.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        }
        else
        {
            await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            messages.Should().Equal("a", "b");
            System.Text.Encoding.UTF8.GetString(await ReadPacket(stream)).Should().EndWith("testreply-a");
            System.Text.Encoding.UTF8.GetString(await ReadPacket(stream)).Should().EndWith("testreply-b");
        }
        // Expected loss warning and, for a rejected handshake, one failed-attempt debug entry.
        logs.TakeErrors().Should().HaveCount(rejectHandshake ? 2 : 1).And.OnlyContain(error => error is IOException);
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Lost_Connection_Is_Reopened_And_Resubscribed(bool rejectFirstRetry, bool subscriberFails)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var logs = new ErrorRecordingLoggerFactory();
        var creation = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            ["test", "central/report"], "write", logs));
        using var broker = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        var firstSubscription = await CompleteHandshake(broker, 2);
        using var transport = await creation.WaitAsync(TimeSpan.FromSeconds(3));
        transport.ReconnectDelays = [TimeSpan.FromMilliseconds(20)];
        var lost = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lostCount = 0;
        var restoredCount = 0;
        var subscriberFailure = new InvalidOperationException("connection subscriber failed");
        if (subscriberFails)
        {
            transport.ConnectionLost += () => throw subscriberFailure;
            transport.ConnectionRestored += () => throw subscriberFailure;
        }
        transport.ConnectionLost += () => { Interlocked.Increment(ref lostCount); lost.TrySetResult(true); };
        transport.ConnectionRestored += () => { Interlocked.Increment(ref restoredCount); restored.TrySetResult(true); };
        transport.OnMessageReceived += (_, message) => received.TrySetResult(message);

        broker.Close();
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Action publish = () => transport.Publish("test", "command");
        publish.Should().Throw<InvalidOperationException>().WithMessage("*lost, reconnecting.*");
        if (rejectFirstRetry)
        {
            using var rejected = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await ReadPacket(rejected.GetStream());
            await rejected.GetStream().WriteAsync(new byte[] { 0x20, 2, 0, 5 });
        }
        using var reconnected = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        var secondSubscription = await CompleteHandshake(reconnected, 2);
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
        secondSubscription.Skip(2).Should().Equal(firstSubscription.Skip(2));
        await reconnected.GetStream().WriteAsync(new byte[] { 0x30, 5, 0, 1, (byte)'t', (byte)'o', (byte)'k' });

        (await received.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be("ok");
        transport.Publish("test", "command");
        var written = await ReadPacket(reconnected.GetStream());
        System.Text.Encoding.UTF8.GetString(written).Should().EndWith("testcommand");
        lostCount.Should().Be(1);
        restoredCount.Should().Be(1);
        var errors = logs.TakeErrors();
        errors.Should().HaveCount((rejectFirstRetry ? 2 : 1) + (subscriberFails ? 2 : 0));
        if (subscriberFails) errors.Count(error => ReferenceEquals(error, subscriberFailure)).Should().Be(2);
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Disposal_Cancels_The_Reconnect_Delay_Without_New_Connections()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var logs = new ErrorRecordingLoggerFactory();
        var creation = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            ["test"], "write", logs));
        using var broker = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await CompleteHandshake(broker, 1);
        using var transport = await creation.WaitAsync(TimeSpan.FromSeconds(3));
        transport.ReconnectDelays = [TimeSpan.FromMilliseconds(100)];
        var lost = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.ConnectionLost += () => lost.TrySetResult(true);
        var acceptedCount = 1;
        using var monitoring = new CancellationTokenSource();
        async Task CountConnections()
        {
            try
            {
                while (true)
                {
                    using var next = await listener.AcceptTcpClientAsync(monitoring.Token);
                    Interlocked.Increment(ref acceptedCount);
                }
            }
            catch (OperationCanceledException) when (monitoring.IsCancellationRequested) { }
        }
        var monitor = CountConnections();
        broker.Close();
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(3));
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(200);

        Volatile.Read(ref acceptedCount).Should().Be(1);
        monitoring.Cancel();
        await monitor.WaitAsync(TimeSpan.FromSeconds(3));
        logs.TakeErrors().Should().ContainSingle();
    }

    [Fact]
    public async Task Disposal_Cancels_A_Reconnect_Handshake()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var logs = new ErrorRecordingLoggerFactory();
        var creation = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            ["test"], "write", logs));
        using var broker = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await CompleteHandshake(broker, 1);
        using var transport = await creation.WaitAsync(TimeSpan.FromSeconds(3));
        transport.ReconnectDelays = [TimeSpan.FromMilliseconds(20)];
        broker.Close();
        using var reconnected = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await ReadPacket(reconnected.GetStream());

        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var buffer = new byte[1];
        (await reconnected.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(2))).Should().Be(0);
        logs.TakeErrors().Should().ContainSingle();
    }

    private static async Task<byte[]> CompleteHandshake(TcpClient broker, int topicCount)
    {
        var stream = broker.GetStream();
        await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 });
        var subscribe = await ReadPacket(stream);
        var acknowledgement = new byte[4 + topicCount];
        acknowledgement[0] = 0x90;
        acknowledgement[1] = (byte)(topicCount + 2);
        acknowledgement[2] = subscribe[0];
        acknowledgement[3] = subscribe[1];
        await stream.WriteAsync(acknowledgement);
        return subscribe;
    }

    [Theory]
    [InlineData("operationTimeout")]
    [InlineData("pingInterval")]
    [InlineData("pingResponseTimeout")]
    public void Invalid_Interval_Identifies_The_Parameter(string parameter)
    {
        using var logs = new ErrorRecordingLoggerFactory();
        Action create = () => new MqttTransport("127.0.0.1", 1, ["test"], "write", logs,
            operationTimeout: parameter == "operationTimeout" ? TimeSpan.Zero : TimeSpan.FromSeconds(1),
            pingInterval: parameter == "pingInterval" ? TimeSpan.Zero : TimeSpan.FromSeconds(1),
            pingResponseTimeout: parameter == "pingResponseTimeout" ? TimeSpan.Zero : TimeSpan.FromSeconds(1));
        var error = create.Should().Throw<ArgumentOutOfRangeException>().Which;
        error.ParamName.Should().Be(parameter);
        error.ActualValue.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Connection_And_Disposal_Handle_Broker_Close(bool brokerCloses)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accepted = listener.AcceptTcpClientAsync();
        using var logs = new ErrorRecordingLoggerFactory();
        var creation = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, ["test"], "write", logs));
        using var broker = await accepted.WaitAsync(TimeSpan.FromSeconds(3));
        var stream = broker.GetStream();
        await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 });
        var subscribe = await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x90, 3, subscribe[0], subscribe[1], 0 });
        var transport = await creation.WaitAsync(TimeSpan.FromSeconds(3));
        if (brokerCloses) broker.Close();
        transport.Dispose();
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Action send = () => transport.SendMessage("test");
        send.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Rejected_Connection_Closes_Socket()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accept = listener.AcceptTcpClientAsync();
        var create = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, ["test"], "write", new ErrorRecordingLoggerFactory()));
        using var broker = await accept;
        var stream = broker.GetStream();
        await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x20, 2, 0, 5 });
        Func<Task> connect = async () => await create;
        await connect.Should().ThrowAsync<IOException>();
        var buffer = new byte[1];
        (await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(3))).Should().Be(0);
    }

    [Fact]
    public async Task Silent_Broker_Handshake_Times_Out_And_Closes_Socket()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accept = listener.AcceptTcpClientAsync();
        var create = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, ["test"], "write", new ErrorRecordingLoggerFactory(), operationTimeout: TimeSpan.FromMilliseconds(500)));
        using var broker = await accept;
        var stream = broker.GetStream();
        await ReadPacket(stream);
        Func<Task> connect = async () => await create.WaitAsync(TimeSpan.FromSeconds(3));
        await connect.Should().ThrowAsync<TimeoutException>();
        var buffer = new byte[1];
        (await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(3))).Should().Be(0);
    }

    [Theory]
    [InlineData(128, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    public async Task Invalid_Subscription_Acknowledgement_Is_Rejected(byte returnCode, bool wrongId)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accept = listener.AcceptTcpClientAsync();
        var create = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, ["test"], "write", new ErrorRecordingLoggerFactory()));
        using var broker = await accept.WaitAsync(TimeSpan.FromSeconds(3));
        var stream = broker.GetStream();
        await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 });
        var subscribe = await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x90, 3, subscribe[0], wrongId ? (byte)(subscribe[1] + 1) : subscribe[1], returnCode });
        Func<Task> connect = async () => await create.WaitAsync(TimeSpan.FromSeconds(3));
        await connect.Should().ThrowAsync<IOException>().WithMessage("*subscription*");
    }

    [Fact]
    public async Task Unrelated_Packets_Do_Not_Extend_Subscription_Deadline()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accept = listener.AcceptTcpClientAsync();
        var create = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            ["test"], "write", new ErrorRecordingLoggerFactory(), operationTimeout: TimeSpan.FromMilliseconds(300)));
        using var broker = await accept.WaitAsync(TimeSpan.FromSeconds(2));
        var stream = broker.GetStream();
        await ReadPacket(stream);
        await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 });
        await ReadPacket(stream);
        var traffic = Task.Run(async () =>
        {
            try
            {
                while (!create.IsCompleted)
                {
                    await stream.WriteAsync(new byte[] { 0xD0, 0 });
                    await Task.Delay(15);
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        });
        Func<Task> connect = async () => await create.WaitAsync(TimeSpan.FromSeconds(2));
        await connect.Should().ThrowAsync<TimeoutException>().WithMessage("*subscription acknowledgement*");
        create.IsCompleted.Should().BeTrue();
        await traffic.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static async Task<byte[]> ReadPacket(NetworkStream stream)
    {
        var header = new byte[2];
        await stream.ReadExactlyAsync(header).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        var body = new byte[header[1]];
        await stream.ReadExactlyAsync(body).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        return body;
    }
}
