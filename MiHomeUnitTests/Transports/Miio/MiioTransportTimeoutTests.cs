using MiHomeLib.MiioDevices.Internal;
using System;
using System.Diagnostics;
using MiHomeLib.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Xunit;

namespace MiHomeUnitTests.Transports.Miio;

[Xunit.Trait("Category", "Integration")]
public class MiioTransportTimeoutTests : IDisposable
{
    private const string TOKEN = "d5e589986c860a6d814ac8aa47240646";

    // Bound but never answers, like a gateway that is not reachable
    private readonly UdpClient _silentDevice = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly MiioTransport _transport;

    public MiioTransportTimeoutTests()
    {
        var port = ((IPEndPoint)_silentDevice.Client.LocalEndPoint).Port;
        _transport = new MiioTransport("127.0.0.1", TOKEN, port)
        {
            ResponseTimeout = TimeSpan.FromMilliseconds(300)
        };
    }

    [Fact]
    public async Task SendMessageAsync_Throws_TimeoutException_When_Device_Does_Not_Respond()
    {
        var act = () => _transport.SendMessageAsync("{}");

        (await act.Should().ThrowAsync<TimeoutException>())
            .WithMessage("*127.0.0.1*did not respond within*");
    }

    [Fact]
    public void SendMessage_Throws_TimeoutException_When_Device_Does_Not_Respond()
    {
        var act = () => _transport.SendMessage("{}");

        act.Should().Throw<TimeoutException>()
            .WithMessage("*127.0.0.1*did not respond within*");
    }

    [Fact]
    public async Task SendMessageAsync_Times_Out_Again_On_Repeated_Call()
    {
        var act = () => _transport.SendMessageAsync("{}");

        await act.Should().ThrowAsync<TimeoutException>();
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Hello_And_Command_Share_One_Response_Timeout()
    {
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(600);
        var elapsed = Stopwatch.StartNew();
        var send = _transport.SendMessageAsync("{\"id\":1,\"method\":\"test\",\"params\":[]}");
        var helloRequest = await _silentDevice.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(400);
        await _silentDevice.SendAsync("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77".ToByteArray(), helloRequest.RemoteEndPoint);
        await _silentDevice.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Func<Task> wait = async () => await send.WaitAsync(TimeSpan.FromSeconds(2));
        await wait.Should().ThrowAsync<TimeoutException>();
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(900));
    }

    public void Dispose()
    {
        _transport.Dispose();
        _silentDevice.Dispose();
    }
}
