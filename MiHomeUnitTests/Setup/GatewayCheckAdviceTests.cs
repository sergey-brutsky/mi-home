using System;
using System.IO;
using System.Net.Sockets;
using MiHomeSetup;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class GatewayCheckAdviceTests
{
    private const string Model = "lumi.gateway.mgl03";
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Closed_Ports_With_Supported_Firmware_Produce_A_Setup_Command_Without_The_Token()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), ClosedPorts(), new GatewayInfo(Model, "1.5.4_0090"), output);
        Assert.Contains("Automatic setup is supported", output.ToString());
        Assert.Contains("make gw setup 192.168.1.100 model=" + Model + " token=\"<MIIO_TOKEN>\"", output.ToString());
        Assert.Contains("make gw check 192.168.1.100", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Fact]
    public void Newer_Firmware_Explains_The_Required_Key()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), ClosedPorts(), new GatewayInfo(Model, "1.5.7_0001"), output);
        Assert.Contains("separate 16-character gateway key", output.ToString());
        Assert.Contains("make gw setup 192.168.1.100 model=" + Model + " token=\"<MIIO_TOKEN>\" key=\"<KEY FROM GW>\"", output.ToString());
    }

    [Fact]
    public void Provided_Inline_Key_Is_Recognized_And_Redacted_In_The_Setup_Command()
    {
        using var output = new StringWriter();
        var options = Options() with { Key = "abcdefghijklmnop" };
        GatewayCheckAdvice.Write(options, ClosedPorts(), new GatewayInfo(Model, "1.5.5"), output);
        Assert.Contains("A gateway key was provided.", output.ToString());
        Assert.Contains("key=\"<KEY FROM GW>\"", output.ToString());
        Assert.Contains("Setup will verify it", output.ToString());
        Assert.DoesNotContain(options.Key, output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Fact]
    public void Provided_Key_File_Is_Used_In_The_Setup_Command()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options() with { KeyFile = "/tmp/my key" }, ClosedPorts(), new GatewayInfo(Model, "1.5.5"), output);
        Assert.Contains("A gateway key was provided.", output.ToString());
        Assert.Contains("key_file='/tmp/my key'", output.ToString());
    }

    [Fact]
    public void Token_File_Path_Is_Preserved_And_Shell_Quoted()
    {
        var options = Options() with { Token = null, TokenFile = "/tmp/token file's path" };
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(options, ClosedPorts(), new GatewayInfo(Model, "1.5.4"), output);
        Assert.Contains("token_file='/tmp/token file'\"'\"'s path'", output.ToString());
    }

    [Fact]
    public void Unknown_Firmware_Provides_Manual_Instructions()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), ClosedPorts(), new GatewayInfo(Model, "1.9.0"), output);
        Assert.Contains("enable Telnet manually", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("make gw setup", output.ToString());
    }

    [Fact]
    public void Missing_Firmware_Requests_An_Authenticated_Check()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), ClosedPorts(), new GatewayInfo(Model, null), output);
        Assert.Contains("eligibility is unknown", output.ToString());
        Assert.Contains("make gw check 192.168.1.100 model=" + Model, output.ToString());
        Assert.DoesNotContain("make gw setup", output.ToString());
    }

    [Fact]
    public void Corrupt_Discovery_Files_Are_Not_Reported_As_Automatically_Repairable()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), new GatewayReadiness(false, false, new InvalidDataException()),
            new GatewayInfo(Model, "1.5.4"), output);
        Assert.Contains("discovery error", output.ToString());
        Assert.DoesNotContain("make gw setup", output.ToString());
    }

    [Fact]
    public void Working_Discovery_Only_Needs_Mqtt_Preparation()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), new GatewayReadiness(false, true), new GatewayInfo(Model, null), output);
        Assert.Contains("make gw setup 192.168.1.100 model=" + Model, output.ToString());
        Assert.DoesNotContain(" token=", output.ToString());
    }

    [Fact]
    public void Ready_Gateway_Needs_No_Repair_Instructions()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), new GatewayReadiness(true, true), new GatewayInfo(Model, "1.5.4"), output);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void Mismatched_Model_Stops_Setup_Advice()
    {
        using var output = new StringWriter();
        GatewayCheckAdvice.Write(Options(), ClosedPorts(), new GatewayInfo("lumi.gateway.mgl001", "1.0.8"), output);
        Assert.Contains("differs from the requested model", output.ToString());
        Assert.DoesNotContain("make gw setup", output.ToString());
    }

    [Fact]
    public void Connection_Error_Messages_Are_Concise()
    {
        var error = new IOException("Long transport explanation", new SocketException((int)SocketError.ConnectionRefused));
        Assert.Equal("Port 1883 is closed or rejecting connections.", GatewayCheckAdvice.DescribeError(error, 1883));
        Assert.DoesNotContain("Long transport explanation", GatewayCheckAdvice.DescribeError(error, 23));
    }

    private static SetupOptions Options() => SetupOptions.Parse(["check", "--host", "192.168.1.100", "--model", Model, "--token", Secret]);

    private static GatewayReadiness ClosedPorts()
    {
        var error = new IOException("Connection refused", new SocketException((int)SocketError.ConnectionRefused));
        return new GatewayReadiness(false, false, error);
    }
}
