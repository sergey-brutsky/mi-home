using MiHomeUnitTests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests.Transports.Mqtt;

[Xunit.Trait("Category", "Integration")]
public class MqttWireTests
{
    [Fact]
    public async Task Write_Ack_Completes_Command_While_Application_Subscriber_Is_Blocked()
    {
        using var session = await Session.Open();
        using var release = new System.Threading.ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Transport.OnMessageReceived += (_, _) => { entered.TrySetResult(true); release.Wait(); };
        try
        {
            await session.Send(0x30, [0, 1, (byte)'t', (byte)'1']);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var command = session.Transport.SendCommandAsync("""{"cmd":"write","did":"plug","params":[{"res_name":"4.1.85","value":1}]}""");
            var packet = await session.Read();
            var length = (packet.body[0] << 8) | packet.body[1];
            var json = Encoding.UTF8.GetString(packet.body, length + 2, packet.body.Length - length - 2);
            var id = System.Text.Json.Nodes.JsonNode.Parse(json)["id"].GetValue<int>();
            var reply = Encoding.UTF8.GetBytes($$"""{"cmd":"write_ack","id":{{id}},"did":"plug","params":[{"res_name":"4.1.85","value":1}]}""");
            await session.Send(0x30, new byte[] { 0, 1, (byte)'t' }.Concat(reply).ToArray());
            await command.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task Delivery_Queue_Overflow_Reports_Connection_Loss()
    {
        using var session = await Session.Open();
        using var release = new System.Threading.ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Transport.ConnectionLost += () => lost.TrySetResult(true);
        session.Transport.OnMessageReceived += (_, _) => { entered.TrySetResult(true); release.Wait(); };
        try
        {
            await session.Send(0x30, [0, 1, (byte)'t', (byte)'1']);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            for (var i = 0; i < 257; i++)
                await session.Send(0x30, [0, 1, (byte)'t', (byte)'2']);
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(2));
            session.Logs.TakeErrors().Should().ContainSingle().Which.Message.Should().Contain("queue is full");
            session.Transport.Dispose();
        }
        finally { release.Set(); }
        await session.Transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Blocked_Subscriber_Allows_Protocol_Processing_And_Ordered_Delivery(bool dispose)
    {
        using var session = await Session.Open();
        using var release = new System.Threading.ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<string>();
        session.Transport.OnMessageReceived += (_, message) =>
        {
            received.Add(message);
            if (message == "1") { entered.SetResult(true); release.Wait(); }
            if (message == "2") finished.TrySetResult(true);
        };
        try
        {
            await session.Send(0x32, [0, 1, (byte)'t', 0, 1, (byte)'1']);
            (await session.Read()).header.Should().Be(0x40);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await session.Send(0x32, [0, 1, (byte)'t', 0, 2, (byte)'2']);
            (await session.Read()).header.Should().Be(0x40);
            await session.Send(0xC0, []);
            (await session.Read()).header.Should().Be(0xD0);
            if (dispose) session.Transport.Dispose();
        }
        finally { release.Set(); }
        if (dispose)
        {
            await session.Transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
            received.Should().Equal("1");
        }
        else
        {
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            received.Should().Equal("1", "2");
        }
    }

    [Fact]
    public async Task Subscriber_Failure_Does_Not_Stop_Other_Subscribers_Acknowledgements_Or_Next_Messages()
    {
        using var session = await Session.Open();
        var received = new List<string>();
        var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Transport.OnMessageReceived += (_, _) => throw new InvalidOperationException("subscriber failure");
        session.Transport.OnMessageReceived += (_, message) => { received.Add(message); if (received.Count == 2) delivered.SetResult(true); };

        for (byte id = 1; id <= 2; id++)
        {
            await session.Send(0x32, [0, 1, (byte)'t', 0, id, (byte)('0' + id)]);
            var ack = await session.Read();
            ack.header.Should().Be(0x40);
            ack.body.Should().Equal(0, id);
        }

        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        received.Should().Equal("1", "2");
        session.Transport.Completion.IsCompleted.Should().BeFalse();
        session.Logs.TakeErrors().Should().HaveCount(2).And.OnlyContain(error => error is InvalidOperationException);
    }

    [Fact]
    public async Task Parallel_Dispose_And_Publish_Send_One_Disconnect_And_Stop_Both_Loops()
    {
        using var session = await Session.Open();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations = Enumerable.Range(0, 32).Select(async index =>
        {
            await start.Task;
            if (index % 3 == 0) session.Transport.Dispose();
            else
            {
                try { session.Transport.Publish("test", "value"); }
                catch (ObjectDisposedException) { }
            }
        }).ToArray();
        start.SetResult(true);
        await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(2));
        await session.Transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var disconnects = 0;
        try
        {
            while (true)
            {
                var packet = await session.Read();
                if (packet.header == 0xE0) disconnects++;
                else packet.header.Should().Be(0x30);
            }
        }
        catch (EndOfStreamException) { }
        disconnects.Should().Be(1);
        session.Logs.Entries.Should().NotContain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error);
        session.Logs.TakeErrors().Where(error => error is not IOException).Should().BeEmpty();
    }

    [Fact]
    public async Task Parallel_Dispose_After_Receive_Failure_Is_Idempotent()
    {
        using var session = await Session.Open();
        session.Broker.Close();
        await session.WaitForConnectionFailure();
        session.Logs.TakeErrors().Should().ContainSingle().Which.Should().BeAssignableTo<IOException>();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(session.Transport.Dispose)));
        Action publish = () => session.Transport.Publish("test", "value");
        publish.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Receive_Failure_Racing_Dispose_Stops_Without_Shutdown_Errors()
    {
        using var session = await Session.Open();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations = Enumerable.Range(0, 16).Select(async index =>
        {
            await start.Task;
            if (index == 0) session.Broker.Close();
            else session.Transport.Dispose();
        }).ToArray();
        start.SetResult(true);
        await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(2));
        await session.Transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        var errors = session.Logs.TakeErrors();
        errors.Where(error => error is not IOException && error is not ObjectDisposedException).Should().BeEmpty();
        session.Logs.Entries.Count(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error)
            .Should().BeLessThanOrEqualTo(1);
    }

    [Theory]
    [InlineData(new byte[] { 0x30, 0x81, 0x80, 0x40 })] // 1 MiB + 1, no body should be read
    [InlineData(new byte[] { 0x30, 0x80, 0x80, 0x80, 0x80 })] // remaining length cannot continue after byte four
    public async Task Invalid_Length_Is_Rejected_Before_Reading_Body(byte[] header)
    {
        using var session = await Session.Open();
        await session.Stream.WriteAsync(header);
        await session.WaitForConnectionFailure();
        session.Logs.TakeErrors().Should().ContainSingle().Which.Should().BeOfType<InvalidDataException>();
    }

    [Fact]
    public async Task Packet_At_Configured_Limit_Is_Accepted()
    {
        using var session = await Session.Open(maxPacketBytes: 64);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Transport.OnMessageReceived += (_, value) => received.SetResult(value);
        var body = new byte[64];
        body[1] = 1;
        body[2] = (byte)'t';
        for (var i = 3; i < body.Length; i++) body[i] = (byte)'x';
        await session.Send(0x30, body);
        (await received.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().Be(new string('x', 61));
        session.Logs.ThrowIfAnyErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Publish_Reaches_Subscriber_And_Qos1_Is_Acknowledged(int qos)
    {
        using var session = await Session.Open();
        var received = new TaskCompletionSource<(string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Transport.OnMessageReceived += (topic, payload) => received.TrySetResult((topic, payload));
        var body = new List<byte> { 0, 4, (byte)'t', (byte)'e', (byte)'s', (byte)'t' };
        if (qos == 1) body.AddRange(new byte[] { 0, 42 });
        body.AddRange(Encoding.UTF8.GetBytes(new string('я', 150)));
        await session.Send((byte)(0x30 | qos << 1), body.ToArray(), fragmented: true);
        (await received.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be(("test", new string('я', 150)));
        if (qos == 1)
        {
            var ack = await session.Read();
            ack.header.Should().Be(0x40);
            ack.body.Should().Equal(0, 42);
        }
        session.Logs.ThrowIfAnyErrors();
    }

    [Fact]
    public async Task Consecutive_Fragmented_Packets_Preserve_Lengths_And_Payloads()
    {
        using var session = await Session.Open();
        var received = new List<string>();
        session.Transport.OnMessageReceived += (_, payload) => received.Add(payload);
        var expected = new[] { "", new string('a', 125), new string('b', 300) };

        for (byte id = 1; id <= expected.Length; id++)
        {
            var body = new List<byte> { 0, 1, (byte)'t', 0, id };
            body.AddRange(Encoding.UTF8.GetBytes(expected[id - 1]));
            await session.Send(0x32, body.ToArray(), fragmented: true);
            var acknowledgement = await session.Read();
            acknowledgement.header.Should().Be(0x40);
            acknowledgement.body.Should().Equal(0, id);
        }

        received.Should().Equal(expected);
        session.Logs.ThrowIfAnyErrors();
    }

    [Fact]
    public async Task Outgoing_Publish_Uses_Default_And_Explicit_Topics()
    {
        using var session = await Session.Open();
        session.Transport.SendMessage("one");
        var first = await session.Read();
        first.header.Should().Be(0x30);
        first.body.Should().Equal(new byte[] { 0, 5, 119, 114, 105, 116, 101, 111, 110, 101 });
        session.Transport.Publish("other", "два");
        var second = await session.Read();
        Encoding.UTF8.GetString(second.body, 2, second.body.Length - 2).Should().Be("otherдва");
        session.Logs.ThrowIfAnyErrors();
    }

    [Theory]
    [InlineData(0x30, new byte[] { 0 })]
    [InlineData(0x30, new byte[] { 0, 10, 1 })]
    [InlineData(0x32, new byte[] { 0, 1, 65 })]
    [InlineData(0x34, new byte[] { 0, 1, 65, 0, 1 })]
    [InlineData(0xE0, new byte[0])]
    public async Task Invalid_Publish_Or_Disconnect_Stops_Both_Loops(byte header, byte[] body)
    {
        using var session = await Session.Open();
        await session.Send(header, body);
        await session.WaitForConnectionFailure();
        var error = session.Logs.TakeErrors().Should().ContainSingle().Which;
        (error is IOException || error is InvalidDataException).Should().BeTrue();
    }

    [Fact]
    public async Task Closed_Connection_In_Middle_Of_Packet_Stops_Receive()
    {
        using var session = await Session.Open();
        await session.Stream.WriteAsync(new byte[] { 0x30, 20, 0 });
        session.Broker.Close();
        await session.WaitForConnectionFailure();
        session.Logs.TakeErrors().Should().ContainSingle();
    }

    [Fact]
    public async Task Ping_Request_Is_Answered()
    {
        using var session = await Session.Open();
        await session.Send(0xC0, []);
        var response = await session.Read();
        response.header.Should().Be(0xD0);
        response.body.Should().BeEmpty();
    }

    [Fact]
    public async Task Ping_Response_Allows_Next_Ping_Then_Missing_Response_Times_Out()
    {
        using var session = await Session.Open(fastKeepAlive: true);
        var first = await session.Read();
        first.header.Should().Be(0xC0);
        await session.Send(0xD0, []);
        var next = await session.Read();
        next.header.Should().Be(0xC0);
        await session.WaitForConnectionFailure();
        session.Logs.TakeErrors().Should().ContainSingle().Which.Should().BeOfType<TimeoutException>();
    }

    private sealed class Session : IDisposable
    {
        public TcpClient Broker;
        public NetworkStream Stream;
        public MqttTransport Transport;
        public ErrorRecordingLoggerFactory Logs = new();
        private readonly TaskCompletionSource<bool> _connectionLost = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitForConnectionFailure()
        {
            // A lost connection now schedules a retry. Dispose after observing the loss to stop it.
            await _connectionLost.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Transport.Dispose();
            await Transport.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        }

        public static async Task<Session> Open(bool fastKeepAlive = false, int maxPacketBytes = 1024 * 1024)
        {
            var session = new Session();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var accept = listener.AcceptTcpClientAsync();
            var create = Task.Run(() => new MqttTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, ["test"], "write", session.Logs,
                pingInterval: fastKeepAlive ? TimeSpan.FromMilliseconds(200) : null,
                pingResponseTimeout: fastKeepAlive ? TimeSpan.FromMilliseconds(600) : null, maxPacketBytes: maxPacketBytes));
            session.Broker = await accept.WaitAsync(TimeSpan.FromSeconds(3));
            session.Stream = session.Broker.GetStream();
            var connect = await session.Read();
            connect.header.Should().Be(0x10);
            // Test-only timing overrides do not change the advertised MQTT keep-alive.
            connect.body[8].Should().Be(0);
            connect.body[9].Should().Be(15);
            await session.Send(0x20, [0, 0]);
            var subscribe = await session.Read();
            await session.Send(0x90, [subscribe.body[0], subscribe.body[1], 0]);
            session.Transport = await create.WaitAsync(TimeSpan.FromSeconds(3));
            session.Transport.ConnectionLost += () => session._connectionLost.TrySetResult(true);
            return session;
        }

        public async Task Send(byte header, byte[] body, bool fragmented = false)
        {
            var packet = new List<byte> { header };
            var remaining = body.Length;
            do
            {
                var value = (byte)(remaining % 128);
                remaining /= 128;
                packet.Add(remaining > 0 ? (byte)(value | 128) : value);
            } while (remaining > 0);
            packet.AddRange(body);
            if (fragmented)
            {
                foreach (var value in packet) await Stream.WriteAsync(new[] { value });
            }
            else await Stream.WriteAsync(packet.ToArray());
        }

        public async Task<(byte header, byte[] body)> Read(TimeSpan? timeout = null)
        {
            using var cancellation = new System.Threading.CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
            var single = new byte[1];
            await Stream.ReadExactlyAsync(single, cancellation.Token);
            var header = single[0];
            var length = 0;
            var multiplier = 1;
            do
            {
                await Stream.ReadExactlyAsync(single, cancellation.Token);
                length += (single[0] & 127) * multiplier;
                multiplier *= 128;
            } while ((single[0] & 128) != 0);
            var body = new byte[length];
            await Stream.ReadExactlyAsync(body, cancellation.Token);
            return (header, body);
        }

        public void Dispose()
        {
            Transport?.Dispose();
            Broker?.Dispose();
        }
    }
}
