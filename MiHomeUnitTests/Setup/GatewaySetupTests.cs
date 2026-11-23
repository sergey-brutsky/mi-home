using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MiHomeSetup;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class GatewaySetupTests
{
    [Theory]
    [InlineData("/data/openmiio_agent miio mqtt cache central z3 --zigbee.tcp=8888", true)]
    [InlineData("/data/openmiio_agent z3 central cache mqtt miio --zigbee.tcp=8888 --log.level=warn", true)]
    [InlineData("/data/openmiio_agent mqtt", false)]
    [InlineData("/tmp/openmiio_agent miio mqtt cache central z3 --zigbee.tcp=8888", false)]
    [InlineData("/data/openmiio_agent miio mqtt cache central z3 --zigbee.tcp=9999", false)]
    [InlineData("", false)]
    public void Running_Agent_Must_Match_The_XiaomiGateway3_Path_And_Options(string command, bool compatible) =>
        Assert.Equal(compatible, GatewayInstaller.IsCompatibleAgent(command));

    [Fact]
    public async Task Ready_Compatible_Agent_Is_Reused_Without_Stopping_Or_Rewriting_Anything()
    {
        var profile = GatewayProfile.Resolve("lumi.gateway.mgl03");
        var commands = new List<string>();
        await WithCommandShellAsync(command =>
        {
            commands.Add(command);
            return (command.Contains("md5sum") ? profile.AgentMd5 + "  /data/openmiio_agent"
                : "/data/openmiio_agent " + GatewayInstaller.AgentArguments, 0);
        }, shell => GatewayInstaller.InstallAsync(shell, profile, _ => Task.FromResult(true), CancellationToken.None,
            download: _ => throw new Exception("A ready compatible installation must not download an agent.")));
        Assert.Equal(2, commands.Count);
        Assert.DoesNotContain(commands, command => command.Contains("killall") || command.Contains("base64") || command.Contains("mv "));
    }

    [Fact]
    public async Task Mqtt_Only_Agent_Is_Restarted_With_The_Full_Integration_Options()
    {
        var profile = GatewayProfile.Resolve("lumi.gateway.mgl03");
        var commands = new List<string>();
        await WithCommandShellAsync(command =>
        {
            commands.Add(command);
            return (command.Contains("md5sum") ? profile.AgentMd5 + "  /data/openmiio_agent"
                : command.StartsWith("for p ") ? "/data/openmiio_agent mqtt" : "", 0);
        }, shell => GatewayInstaller.InstallAsync(shell, profile, _ => Task.FromResult(true), CancellationToken.None, TimeSpan.Zero));
        Assert.Contains(commands, command => command.Contains("killall openmiio_agent"));
        Assert.Contains(commands, command => command.Contains("/data/openmiio_agent " + GatewayInstaller.AgentArguments));
        Assert.DoesNotContain(commands, command => command.Contains("base64"));
    }

    [Theory]
    [InlineData("lumi.gateway.mgl03", "mips", "admin")]
    [InlineData("lumi.gateway.mcn001", "arm", "root")]
    [InlineData("lumi.gateway.mgl001", "arm", "root")]
    [InlineData("lumi.gateway.aqcn02", "arm", "root")]
    public void Profiles_Select_The_Correct_Binary_And_Login(string model, string architecture, string login)
    {
        var profile = GatewayProfile.Resolve(model);
        Assert.Equal(architecture, profile.Architecture);
        Assert.Equal(login, profile.Login);
    }

    [Fact]
    public void Unknown_Models_And_Corrupt_Agents_Are_Rejected()
    {
        Assert.Throws<ArgumentException>(() => GatewayProfile.Resolve("unknown"));
        Assert.Throws<InvalidDataException>(() => GatewayInstaller.ValidateBinary(new byte[2048], GatewayProfile.Resolve("lumi.gateway.mgl03")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Shell_Handles_Command_Echo_Fragmented_Prompts_And_Exit_Status(int exitCode)
    {
        await WithShellAsync(_ => ("value # inside output", exitCode), async shell =>
        {
            if (exitCode == 0)
                Assert.Equal("value # inside output", await shell.RunAsync("echo value", CancellationToken.None));
            else
                await Assert.ThrowsAsync<IOException>(() => shell.RunAsync("false", CancellationToken.None));
        });
    }

    [Fact]
    public async Task Wrong_Model_Stops_Before_Installation()
    {
        var commands = new List<string>();
        await WithCommandShellAsync(command => { commands.Add(command); return ("wrong-model", 0); }, async shell =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => GatewayInstaller.VerifyAsync(shell,
                GatewayProfile.Resolve("lumi.gateway.mgl03"), "lumi.gateway.mgl03", CancellationToken.None)));
        Assert.Single(commands);
        Assert.Contains("device.conf", commands[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Existing_Verified_Agent_Is_Reused_And_Failure_Attempts_Recovery(bool healthy)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var profile = GatewayProfile.Resolve("lumi.gateway.mgl03");
        var commands = new List<string>();
        var checks = 0;
        await WithCommandShellAsync(command =>
        {
            commands.Add(command);
            return (command.Contains("md5sum") ? profile.AgentMd5 + "  /data/openmiio_agent" : "", 0);
        }, async shell =>
        {
            Task Install() => GatewayInstaller.InstallAsync(shell, profile, _ =>
            {
                checks++;
                return Task.FromResult(healthy);
            }, CancellationToken.None, TimeSpan.Zero, output: output, error: error);
            if (healthy) await Install();
            else await Assert.ThrowsAsync<IOException>(Install);
        });
        Assert.Equal(healthy ? 1 : 5, checks);
        Assert.Contains("Preparing the XiaomiGateway3-compatible agent.", output.ToString());
        if (healthy) Assert.Contains("Setup complete.", output.ToString());
        else Assert.Contains("Recovery attempted.", error.ToString());
        Assert.DoesNotContain(commands, command => command.Contains("base64") || command.Contains("mv "));
        Assert.Equal(healthy ? 1 : 2, commands.Count(command => command.Contains("/data/openmiio_agent " + GatewayInstaller.AgentArguments)));
        Assert.Equal(healthy ? 0 : 1, commands.Count(command => command.Contains("killall mosquitto")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Replacement_Is_Verified_Before_Stopping_Services_And_Restored_On_Failure(bool healthy)
    {
        var binary = new byte[2048];
        var checksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(binary)).ToLowerInvariant();
        var profile = GatewayProfile.Resolve("lumi.gateway.mgl03") with { AgentMd5 = checksum };
        var commands = new List<string>();
        await WithCommandShellAsync(command =>
        {
            commands.Add(command);
            if (command.Contains("md5sum /data/openmiio_agent.mihome-")) return (checksum + "  temporary", 0);
            if (command.Contains("echo yes")) return ("yes", 0);
            return ("", 0);
        }, async shell =>
        {
            Task Install() => GatewayInstaller.InstallAsync(shell, profile, _ => Task.FromResult(healthy),
                CancellationToken.None, TimeSpan.Zero, _ => Task.FromResult(binary));
            if (healthy) await Install();
            else await Assert.ThrowsAsync<IOException>(Install);
        });
        Assert.True(commands.FindIndex(command => command.Contains("md5sum /data/openmiio_agent.mihome-")) <
            commands.FindIndex(command => command.Contains("killall")));
        Assert.Contains(commands, command => command.Contains("cp /data/openmiio_agent"));
        Assert.Equal(!healthy, commands.Any(command => command.Contains(".backup /data/openmiio_agent")));
        Assert.Contains("rm -f /data/openmiio_agent.mihome-", commands.Last());
        Assert.DoesNotContain(commands, command => command.Contains("/etc/") || command.Contains("/bin/"));
        Assert.Contains(commands, command => command.Contains("/data/openmiio_agent " + GatewayInstaller.AgentArguments));
    }

    [Fact]
    public async Task Invalid_Upload_Is_Rejected_Before_Stopping_Processes()
    {
        var binary = new byte[2048];
        var checksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(binary)).ToLowerInvariant();
        var profile = GatewayProfile.Resolve("lumi.gateway.mgl03") with { AgentMd5 = checksum };
        var commands = new List<string>();
        await WithCommandShellAsync(command => { commands.Add(command); return ("", 0); }, async shell =>
            await Assert.ThrowsAsync<InvalidDataException>(() => GatewayInstaller.InstallAsync(shell, profile,
                _ => Task.FromResult(true), CancellationToken.None, TimeSpan.Zero, _ => Task.FromResult(binary))));
        Assert.DoesNotContain(commands, command => command.Contains("killall") || command.Contains("mv "));
        Assert.DoesNotContain(commands, command => command.Contains("/etc/") || command.Contains("/bin/"));
        Assert.Contains("rm -f /data/openmiio_agent.mihome-", commands.Last());
    }

    [Fact]
    public async Task Shell_Login_Can_Be_Cancelled_When_Peer_Is_Silent()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var shell = new GatewayShell();
        using var cancellation = new CancellationTokenSource();
        var connect = shell.ConnectAsync("127.0.0.1", GatewayProfile.Resolve("lumi.gateway.mgl03"),
            cancellation.Token, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connect);
    }

    private static Task WithCommandShellAsync(Func<string, (string Output, int Status)> respond, Func<IGatewayShell, Task> test) =>
        test(new CommandShell(respond));

    private sealed class CommandShell(Func<string, (string Output, int Status)> respond) : IGatewayShell
    {
        public Task<string> RunAsync(string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var response = respond(command);
            if (response.Status != 0) throw new IOException("Gateway command failed.");
            return Task.FromResult(response.Output);
        }
    }

    private static async Task WithShellAsync(Func<string, (string Output, int Status)> respond, Func<GatewayShell, Task> test)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = peer.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("login: "), deadline.Token);
            Assert.Equal("admin", await reader.ReadLineAsync(deadline.Token));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("admin\r\n# "), deadline.Token);
            while (await reader.ReadLineAsync(deadline.Token) is { } command)
            {
                var markerId = Regex.Match(command, @"[a-f0-9]{32}").Value;
                Assert.NotEmpty(markerId);
                var marker = "MIHOME_" + markerId;
                var reply = respond(command);
                var response = command + "\r\n" + marker + "_BEGIN\r\n" + reply.Output + "\r\n" + marker + "_END:" + reply.Status + "\r\n#";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), deadline.Token);
                await stream.WriteAsync(new byte[] { 32 }, deadline.Token);
            }
        });
        try
        {
            using var shell = new GatewayShell();
            await shell.ConnectAsync("127.0.0.1", GatewayProfile.Resolve("lumi.gateway.mgl03"), deadline.Token,
                ((IPEndPoint)listener.LocalEndpoint).Port);
            await test(shell);
        }
        finally
        {
            await server.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }
}
