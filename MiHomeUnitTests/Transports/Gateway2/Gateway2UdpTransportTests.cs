using MiHomeUnitTests.Infrastructure;
using MiHomeLib.Helpers;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Transports.Gateway2;

[Xunit.Trait("Category", "Integration")]
public class Gateway2UdpTransportTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public async Task Socket_Errors_Are_Retried_And_Success_Resets_The_Error_Count(int errorsPerPacket)
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)socket.Client.LocalEndPoint;
        using var logs = new ErrorRecordingLoggerFactory();
        using var transport = new UdpTransport(socket, endpoint, logs) { ReceiveErrorDelay = TimeSpan.Zero };
        var calls = 0;
        transport.Receive = () =>
        {
            var index = calls++;
            if (index >= (errorsPerPacket + 1) * 2) return socket.ReceiveAsync();
            if (index % (errorsPerPacket + 1) < errorsPerPacket)
                throw new SocketException((int)SocketError.ConnectionReset);
            return Task.FromResult(new UdpReceiveResult(Encoding.UTF8.GetBytes("recovered"), endpoint));
        };
        var received = 0;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.OnMessageReceived += message =>
        {
            if (message == "recovered" && ++received == 2) completed.TrySetResult(true);
        };
        using var sender = new UdpClient();
        // Release the real receive started by the constructor before replacing Receive.
        await sender.SendAsync(new byte[] { 0 }, 1, endpoint);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        transport.Completion.IsCompleted.Should().BeFalse();
        transport.SendDiscover();
        logs.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            entry.Message == "Gateway 2 UDP receive failed, receiving continues");
        // The recording logger also collects exceptions attached to expected warnings.
        logs.TakeErrors().Should().HaveCount(errorsPerPacket * 2).And.OnlyContain(error => error is SocketException);
        logs.Entries.Should().NotContain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error);
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Ten_Consecutive_Socket_Errors_Stop_And_Dispose_The_Transport()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)socket.Client.LocalEndPoint;
        using var logs = new ErrorRecordingLoggerFactory();
        using var transport = new UdpTransport(socket, endpoint, logs) { ReceiveErrorDelay = TimeSpan.Zero };
        var calls = 0;
        transport.Receive = () =>
        {
            calls++;
            throw new SocketException((int)SocketError.ConnectionReset);
        };
        using var sender = new UdpClient();
        await sender.SendAsync(new byte[] { 0 }, 1, endpoint);

        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        calls.Should().Be(10);
        Action send = transport.SendDiscover;
        send.Should().Throw<ObjectDisposedException>();
        logs.TakeErrors().Should().HaveCount(10).And.OnlyContain(error => error is SocketException);
        logs.Entries.Should().ContainSingle(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error &&
            entry.Message == "Gateway 2 UDP receive loop stopped.");
    }

    [Fact]
    public async Task Commands_Are_Sent_As_Actual_Udp_Datagrams()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var transport = new UdpTransport(socket, (IPEndPoint)receiver.Client.LocalEndPoint, null);
        transport.SendDiscover();
        Encoding.UTF8.GetString((await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer)
            .Should().Be("{\"cmd\":\"get_id_list\"}");
        transport.SendRead("sensor");
        Encoding.UTF8.GetString((await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer)
            .Should().Be("{\"cmd\":\"read\",\"sid\":\"sensor\"}");
        transport.Token = "0123456789abcdef";
        transport.SendWrite("sensor", "plug", "0123456789abcdef", ("status", "on"));
        var json = System.Text.Json.Nodes.JsonNode.Parse(Encoding.UTF8.GetString((await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer));
        json["cmd"].GetValue<string>().Should().Be("write");
        json["data"]["status"].GetValue<string>().Should().Be("on");
        json["data"]["key"].GetValue<string>().Should().Be(MiHomeLib.XiaomiGateway2.Gateway2KeyBuilder.BuildKey(transport.Token, "0123456789abcdef").ToHex());
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Multicast_Socket_Can_Be_Created_And_Closed()
    {
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        var port = ((IPEndPoint)reservation.Client.LocalEndPoint).Port;
        reservation.Close();
        using var transport = new UdpTransport(serverPort: port);
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Subscriber_Failure_Does_Not_Block_Other_Subscribers_Or_Later_Packets()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)socket.Client.LocalEndPoint;
        using var logs = new ErrorRecordingLoggerFactory();
        using var transport = new UdpTransport(socket, endpoint, logs);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.OnMessageReceived += _ => throw new InvalidOperationException("subscriber failed");
        transport.OnMessageReceived += value => received.TrySetResult(value);
        using var sender = new UdpClient();
        for (var i = 0; i < 2; i++)
        {
            received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bytes = Encoding.UTF8.GetBytes("пакет");
            await sender.SendAsync(bytes, bytes.Length, endpoint);
            (await received.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().Be("пакет");
        }
        transport.Dispose();
        transport.Dispose();
        await transport.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        logs.TakeErrors().Should().HaveCount(2);
    }
}
