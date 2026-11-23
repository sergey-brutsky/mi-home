using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.MiioDevices.Internal;
using MiHomeSetup;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class TelnetActivatorTests
{
    private const string Model = "lumi.gateway.mgl03";
    private const string Key = "0123456789abcdef";

    [Theory]
    [InlineData("lumi.gateway.mgl03", "1.4.6_0042", "enable_telnet_service")]
    [InlineData("lumi.gateway.mgl03", "1.4.6_0043", "set_ip_info")]
    [InlineData("lumi.gateway.mgl03", "1.4.6_10000", "set_ip_info")]
    [InlineData("lumi.gateway.mgl03", "1.4.7_0001", "set_ip_info")]
    [InlineData("lumi.gateway.mgl03", "1.5.0", "set_ip_info")]
    [InlineData("lumi.gateway.mgl03", "1.5.4_0090", "set_ip_info")]
    [InlineData("lumi.gateway.mgl03", "1.5.5_0006", "system_command")]
    [InlineData("lumi.gateway.mgl03", "1.5.7", "system_command")]
    [InlineData("lumi.gateway.mcn001", "1.0.3", "set_ip_info")]
    [InlineData("lumi.gateway.mcn001", "1.0.6_0030", "set_ip_info")]
    [InlineData("lumi.gateway.mcn001", "1.0.7_0019", "system_command")]
    [InlineData("lumi.gateway.mgl001", "1.0.8", "system_command")]
    [InlineData("lumi.gateway.aqcn02", "4.0.1", "set_ip_info")]
    [InlineData("lumi.gateway.aqcn02", "4.0.4", "system_command")]
    public async Task Known_Firmware_Selects_One_Command_With_Explicit_Retry_Policy(string model, string firmware, string method)
    {
        using var output = new StringWriter();
        var miio = CreateMiio(model, firmware);
        object parameters = null;
        miio.Setup(x => x.RequestAsync(method, It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat))
            .Callback<string, object, MiioRetryPolicy>((_, value, _) => parameters = value)
            .ReturnsAsync("{\"result\":[\"ok\"]}");

        await TelnetActivator.EnableAsync(model, miio.Object, () => 67283024, Key,
            _ => Task.FromResult(true), CancellationToken.None, output: output);

        Assert.Contains("miIO model: " + model, output.ToString());
        Assert.Contains("Telnet port is accessible.", output.ToString());

        if (method == "enable_telnet_service") Assert.Null(parameters);
        else
        {
            var json = JsonSerializer.SerializeToNode(parameters, MiioJson.Options);
            if (method == "set_ip_info")
            {
                Assert.Equal("\"\"", json["ssid"].GetValue<string>());
                Assert.EndsWith("; telnetd", json["pswd"].GetValue<string>());
            }
            else
            {
                Assert.Equal("vWbAD7ijiEq8mZs=", json["password"].GetValue<string>());
                Assert.EndsWith("; telnetd", json["command"].GetValue<string>());
            }
        }
        miio.Verify(x => x.RequestAsync(method, It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat), Times.Once);
        miio.Verify(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat), Times.Once);
        miio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("1.5.8")]
    [InlineData("1.5.10")]
    [InlineData("1.4.8")]
    [InlineData("2.0.0")]
    [InlineData("1.4.6")]
    public async Task Unknown_Or_Ambiguous_Firmware_Stops_After_Info(string firmware)
    {
        var miio = CreateMiio(Model, firmware);
        await Assert.ThrowsAsync<InvalidOperationException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, Key, _ => throw new Exception("Must not probe after unsupported firmware"), CancellationToken.None));
        Assert.Single(miio.Invocations);
    }

    [Theory]
    [InlineData("unknown", "1.5.4")]
    [InlineData("lumi.gateway.mcn001", "1.0.6")]
    public async Task Model_Mismatch_Stops_Before_Activation(string actualModel, string firmware)
    {
        var miio = CreateMiio(actualModel, firmware);
        await Assert.ThrowsAsync<InvalidOperationException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, Key, _ => Task.FromResult(true), CancellationToken.None));
        Assert.Single(miio.Invocations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("too-short")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public async Task Newer_Firmware_Requires_A_Separate_Valid_Key(string key)
    {
        var miio = CreateMiio(Model, "1.5.5_0006");
        var error = await Record.ExceptionAsync(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, key, _ => Task.FromResult(true), CancellationToken.None));
        Assert.True(error is ArgumentException or InvalidOperationException);
        Assert.Single(miio.Invocations);
        if (key is not null) Assert.DoesNotContain(key, error.Message);
    }

    [Theory]
    [InlineData("", 1u)]
    [InlineData("54:EF:44:12:34", 1u)]
    [InlineData("54:EF:44:12:34:56", null)]
    public async Task Newer_Firmware_Requires_Complete_Identity(string mac, uint? deviceId)
    {
        var miio = CreateMiio(Model, "1.5.5", mac);
        await Assert.ThrowsAsync<InvalidDataException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => deviceId, Key, _ => Task.FromResult(true), CancellationToken.None));
        Assert.Single(miio.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_Acknowledgement_Does_Not_Replay_Activation_And_Port_Is_Checked(bool opens)
    {
        var miio = CreateMiio(Model, "1.5.4");
        miio.Setup(x => x.RequestAsync("set_ip_info", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat))
            .ThrowsAsync(new TimeoutException("Response lost"));
        var probes = 0;
        Task Enable() => TelnetActivator.EnableAsync(Model, miio.Object, () => 1, null,
            _ => { probes++; return Task.FromResult(opens); }, CancellationToken.None, TimeSpan.Zero);
        if (opens) await Enable();
        else await Assert.ThrowsAsync<IOException>(Enable);
        Assert.Equal(opens ? 1 : 5, probes);
        miio.Verify(x => x.RequestAsync("set_ip_info", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat), Times.Once);
    }

    [Theory]
    [InlineData("{\"result\":[\"ok\"]}", "acknowledged")]
    [InlineData("{\"error\":{\"code\":-4004,\"message\":\"secret-value\"}}", "not confirmed")]
    public async Task Acknowledgement_Alone_Does_Not_Prove_Telnet_Readiness(string response, string message)
    {
        var miio = CreateMiio(Model, "1.5.4");
        miio.Setup(x => x.RequestAsync("set_ip_info", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat)).ReturnsAsync(response);
        var error = await Assert.ThrowsAsync<IOException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, null, _ => Task.FromResult(false), CancellationToken.None, TimeSpan.Zero));
        Assert.Contains(message, error.Message);
        Assert.DoesNotContain("secret-value", error.Message);
    }

    [Fact]
    public async Task Cancellation_Stops_Before_Sending_Anything()
    {
        var miio = new Mock<IMiioRequests>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, null, _ => Task.FromResult(false), cancellation.Token));
        Assert.Empty(miio.Invocations);
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("[]")]
    [InlineData("{\"result\":null}")]
    public async Task Malformed_Activation_Acknowledgement_Still_Checks_The_Port(string response)
    {
        var miio = CreateMiio(Model, "1.5.4");
        miio.Setup(x => x.RequestAsync("set_ip_info", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat)).ReturnsAsync(response);
        await TelnetActivator.EnableAsync(Model, miio.Object, () => 1, null,
            _ => Task.FromResult(true), CancellationToken.None);
        miio.Verify(x => x.RequestAsync("set_ip_info", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat), Times.Once);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"result\":null}")]
    [InlineData("{\"result\":{\"model\":\"lumi.gateway.mgl03\"}}")]
    public async Task Incomplete_Gateway_Information_Stops_Activation(string response)
    {
        var miio = new Mock<IMiioRequests>(MockBehavior.Strict);
        miio.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat)).ReturnsAsync(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, null, _ => Task.FromResult(true), CancellationToken.None));
        Assert.Single(miio.Invocations);
    }

    [Fact]
    public async Task Cancellation_After_Gateway_Info_Stops_Before_Activation()
    {
        using var cancellation = new CancellationTokenSource();
        var miio = CreateMiio(Model, "1.5.4");
        miio.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat))
            .Callback(cancellation.Cancel)
            .ReturnsAsync(JsonSerializer.Serialize(new { result = new { model = Model, fw_ver = "1.5.4" } }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TelnetActivator.EnableAsync(Model, miio.Object,
            () => 1, null, _ => Task.FromResult(true), cancellation.Token));
        Assert.Single(miio.Invocations);
    }

    [Theory]
    [InlineData(67283024u, "54:EF:44:12:34:56", "0123456789abcdef", "vWbAD7ijiEq8mZs=")]
    [InlineData(uint.MaxValue, "AA:BB:CC:DD:EE:FF", "abcdefghijklmnop", "17xGTjQk+615TTk=")]
    public void Password_Matches_Independent_Python_Vectors(uint did, string mac, string key, string expected) =>
        Assert.Equal(expected, TelnetActivator.CreatePassword(did, mac, key));

    private static Mock<IMiioRequests> CreateMiio(string model, string firmware, string mac = "54:EF:44:12:34:56")
    {
        var miio = new Mock<IMiioRequests>(MockBehavior.Strict);
        var response = JsonSerializer.Serialize(new { result = new { model, fw_ver = firmware, mac } });
        miio.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat)).ReturnsAsync(response);
        return miio;
    }
}
