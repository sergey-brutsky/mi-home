using MiHomeLib.MiioDevices.Internal;
using System;
using MiHomeLib.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
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
        var responseTimeout = TimeSpan.FromMilliseconds(600);
        var helloDuration = TimeSpan.FromMilliseconds(400);
        var testTimeout = TimeSpan.FromSeconds(10);
        var utcTicks = DateTime.UnixEpoch.Ticks;
        var waits = Channel.CreateUnbounded<(TimeSpan Duration, TaskCompletionSource Expire)>();
        _transport.ResponseTimeout = responseTimeout;
        _transport.GetUtcNow = () => new DateTime(Interlocked.Read(ref utcTicks), DateTimeKind.Utc);
        _transport.WaitForReceiveTimeoutAsync = (duration, cancellationToken) =>
        {
            var expire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waits.Writer.TryWrite((duration, expire));
            return expire.Task.WaitAsync(cancellationToken);
        };

        var send = _transport.SendMessageAsync("{\"id\":1,\"method\":\"test\",\"params\":[]}");
        var helloRequest = await _silentDevice.ReceiveAsync().WaitAsync(testTimeout);
        var helloWait = await waits.Reader.ReadAsync().AsTask().WaitAsync(testTimeout);
        helloWait.Duration.Should().Be(responseTimeout);
        Interlocked.Add(ref utcTicks, helloDuration.Ticks);
        await _silentDevice.SendAsync("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77".ToByteArray(), helloRequest.RemoteEndPoint);

        var command = await _silentDevice.ReceiveAsync().WaitAsync(testTimeout);
        command.Buffer.Length.Should().BeGreaterThan(32);
        var commandWait = await waits.Reader.ReadAsync().AsTask().WaitAsync(testTimeout);
        commandWait.Duration.Should().Be(responseTimeout - helloDuration);

        Interlocked.Add(ref utcTicks, commandWait.Duration.Ticks);
        commandWait.Expire.SetResult();
        Func<Task> wait = () => send.WaitAsync(testTimeout);
        (await wait.Should().ThrowAsync<TimeoutException>()).WithMessage("*(1 attempts)*outcome is unknown*");
    }

    public void Dispose()
    {
        _transport.Dispose();
        _silentDevice.Dispose();
    }
}
