using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace MiHomeLib.MqttGateway;

/// <summary>
/// Opens TCP connections to the gateway and turns socket errors into messages that say what to check
/// </summary>
internal static class GatewayConnection
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <param name="service">Human readable name of the gateway service, e.g. "MQTT" or "telnet"</param>
    /// <param name="enableHint">How the service gets enabled on the gateway, appended when the port is closed</param>
    public static void Connect(TcpClient client, string ip, int port, string service, string enableHint) =>
        ConnectAsync(client, ip, port, service, enableHint).GetAwaiter().GetResult();

    internal static async Task ConnectAsync(TcpClient client, string ip, int port, string service, string enableHint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using (cancellationToken.Register(client.Dispose))
        {
            try
            {
                await ConnectAsync(client.ConnectAsync(ip, port), ip, port, service, enableHint,
                    ConnectTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }

    internal static void Connect(Task connect, string ip, int port, string service, string enableHint, TimeSpan timeout) =>
        ConnectAsync(connect, ip, port, service, enableHint, timeout).GetAwaiter().GetResult();

    internal static async Task ConnectAsync(Task connect, string ip, int port, string service, string enableHint,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (await Task.WhenAny(connect, Task.Delay(timeout, delayCancellation.Token)).ConfigureAwait(false) != connect)
        {
            // The abandoned attempt fails when the client is closed, its exception is observed here.
            _ = connect.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(NotReachable(ip, port, service, $"no answer within {timeout.TotalSeconds} seconds"));
        }
        delayCancellation.Cancel();

        try
        {
            await connect.ConfigureAwait(false);
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused)
        {
            throw new IOException(
                $"Gateway '{ip}' refused the {service} connection on port {port}. The address answers, but nothing accepts " +
                $"connections on this port. Make sure '{ip}' is the gateway's address and not another device in this network, " +
                $"{service} is enabled on the gateway{enableHint} and port {port} is not blocked by a firewall.", e);
        }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.TimedOut or SocketError.HostUnreachable
                                            or SocketError.NetworkUnreachable or SocketError.HostDown)
        {
            throw new TimeoutException(NotReachable(ip, port, service, e.Message), e);
        }
    }

    private static string NotReachable(string ip, int port, string service, string reason) =>
        $"Gateway '{ip}' is not reachable on {service} port {port} ({reason}). Make sure the gateway is powered on, " +
        $"this machine is in the same network as the gateway or connected to it via VPN, " +
        $"and port {port} is not blocked by a firewall.";
}
