using System;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests.Discovery;

public class GatewayConnectionTests
{
    [Fact]
    public async Task Pending_Async_Connect_Can_Be_Cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = GatewayConnection.ConnectAsync(pending.Task, "127.0.0.1", 123, "telnet", "",
            TimeSpan.FromSeconds(5), cancellation.Token);
        cancellation.Cancel();
        Func<Task> wait = async () => await connection.WaitAsync(TimeSpan.FromSeconds(1));
        await wait.Should().ThrowAsync<OperationCanceledException>();
        pending.SetException(new SocketException((int)SocketError.OperationAborted));
    }

    [Theory]
    [InlineData(SocketError.TimedOut)]
    [InlineData(SocketError.HostUnreachable)]
    [InlineData(SocketError.NetworkUnreachable)]
    [InlineData(SocketError.HostDown)]
    public void Unreachable_Socket_Error_Has_Endpoint_And_Hint(SocketError code)
    {
        Action connect = () => GatewayConnection.Connect(Task.FromException(new SocketException((int)code)),
            "127.0.0.1", 123, "MQTT", "", TimeSpan.FromSeconds(1));
        connect.Should().Throw<TimeoutException>().WithMessage("*127.0.0.1*MQTT*123*VPN*");
    }

    [Fact]
    public void Pending_Connect_Stops_At_Deadline()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action connect = () => GatewayConnection.Connect(pending.Task, "127.0.0.1", 123, "telnet", "", TimeSpan.FromMilliseconds(20));
        connect.Should().Throw<TimeoutException>().WithMessage("*telnet*no answer within*");
        pending.SetException(new SocketException((int)SocketError.OperationAborted));
    }

    [Fact]
    public void Closed_Port_Gives_Message_With_What_To_Check()
    {
        // A port that was just free and is closed again refuses connections
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        using var client = new TcpClient();
        var act = () => GatewayConnection.Connect(client, "127.0.0.1", port, "MQTT", "");

        act.Should().Throw<IOException>()
            .WithMessage($"Gateway '127.0.0.1' refused the MQTT connection on port {port}*firewall*")
            .WithInnerException<SocketException>();
    }

    [Fact]
    public void Open_Port_Connects()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            GatewayConnection.Connect(client, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "MQTT", "");
            client.Connected.Should().BeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }
}
