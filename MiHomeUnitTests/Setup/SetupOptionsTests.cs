using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MiHomeSetup;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class SetupOptionsTests
{
    [Theory]
    [InlineData("check")]
    [InlineData("setup")]
    [InlineData("disable-lan")]
    public void Gateway2_Is_Accepted_For_Native_Protocol_Commands(string command)
    {
        var options = SetupOptions.Parse([command, "--host", "127.0.0.1", "--model", "lumi.gateway.v3",
            "--token", "0123456789abcdef0123456789abcdef"]);
        Assert.Equal("lumi.gateway.v3", options.Model);
    }

    [Fact]
    public void Disable_Lan_Allows_Detection_But_Rejects_Mqtt_Models_And_Key_Options()
    {
        Assert.Null(SetupOptions.Parse(["disable-lan", "--host", "127.0.0.1", "--token-file", "gateway.token"]).Model);
        Assert.Throws<ArgumentException>(() => SetupOptions.Parse(["disable-lan", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03"]));
        Assert.Throws<ArgumentException>(() => SetupOptions.Parse(["disable-lan", "--host", "127.0.0.1", "--key", "abcdefghijklmnop"]));
    }

    [Fact]
    public void Gateway2_Does_Not_Select_An_Mqtt_Agent_Binary() =>
        Assert.Throws<ArgumentException>(() => SetupOptions.Parse(["download-agent", "--model", "lumi.gateway.v3", "--output", "agent.bin"]));

    [Theory]
    [InlineData("check")]
    [InlineData("setup")]
    public async Task Commands_Accept_An_Inline_Key(string command)
    {
        var options = SetupOptions.Parse([command, "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03",
            "--token", "0123456789abcdef0123456789abcdef", "--key", "abcdefghijklmnop"]);
        Assert.Equal("abcdefghijklmnop", await options.ReadKeyAsync(CancellationToken.None));
        Assert.Null(options.KeyFile);
    }

    [Fact]
    public async Task Check_Accepts_A_Key_File_And_Automatic_Model_Detection()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "abcdefghijklmnop\n");
            var options = SetupOptions.Parse(["check", "--host", "127.0.0.1",
                "--token-file", "gateway.token", "--key-file", path]);
            Assert.Null(options.Model);
            Assert.Equal("abcdefghijklmnop", await options.ReadKeyAsync(CancellationToken.None));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("abc\tdefghijklmno")]
    public async Task Invalid_Inline_Keys_Are_Rejected_Without_Echoing_Them(string key)
    {
        var options = SetupOptions.Parse(["check", "--host", "127.0.0.1",
            "--token", "0123456789abcdef0123456789abcdef", "--key", key]);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => options.ReadKeyAsync(CancellationToken.None));
        Assert.DoesNotContain(key, error.Message);
    }

    [Theory]
    [InlineData("check", false)]
    [InlineData("check", true)]
    [InlineData("setup", false)]
    [InlineData("setup", true)]
    public void Check_And_Setup_Allow_Model_Detection_With_An_Optional_Token(string command, bool withToken)
    {
        var args = new List<string> { command, "--host", "127.0.0.1" };
        if (withToken) args.AddRange(["--token", "0123456789abcdef0123456789abcdef"]);
        var options = SetupOptions.Parse(args.ToArray());
        Assert.Null(options.Model);
        Assert.Equal(withToken, options.Token is not null);
    }

    [Fact]
    public void Download_Agent_Requires_An_Explicit_Model() =>
        Assert.Throws<ArgumentException>(() => SetupOptions.Parse(["download-agent", "--output", "agent.bin"]));

    [Fact]
    public void Setup_Preserves_Credentials_For_Automatic_Model_Detection()
    {
        var options = SetupOptions.Parse(["setup", "--host", "192.168.1.100", "--token-file", "gateway.token", "--key-file", "gateway.key"]);
        Assert.Null(options.Model);
        Assert.Equal("gateway.token", options.TokenFile);
        Assert.Equal("gateway.key", options.KeyFile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Setup_Accepts_An_Inline_Token_With_An_Optional_Key_File(bool withKey)
    {
        const string token = "0123456789abcdef0123456789ABCDEF";
        var args = new List<string> { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token", token };
        if (withKey) args.AddRange(["--key-file", "gateway.key"]);

        var options = SetupOptions.Parse(args.ToArray());

        Assert.Equal(token, options.Token);
        Assert.Null(options.TokenFile);
        Assert.Equal(withKey ? "gateway.key" : null, options.KeyFile);
        Assert.Equal(token, await options.ReadTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Setup_Still_Reads_A_Token_From_A_File_With_A_Trailing_Newline()
    {
        const string token = "0123456789abcdef0123456789abcdef";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, token + "\n");
            var options = SetupOptions.Parse(["setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token-file", path]);
            Assert.Equal(token, await options.ReadTokenAsync(CancellationToken.None));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("0123456789abcdef0123456789abcdef00")]
    [InlineData("gggggggggggggggggggggggggggggggg")]
    public async Task Invalid_Inline_Token_Is_Rejected_Without_Echoing_It(string token)
    {
        var options = SetupOptions.Parse(["setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token", token]);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => options.ReadTokenAsync(CancellationToken.None));
        Assert.DoesNotContain(token, error.Message);
    }

    [Fact]
    public void Setup_Accepts_Credential_Paths_In_Any_Order()
    {
        var options = SetupOptions.Parse(["setup", "--key-file", "gateway.key", "--model", "lumi.gateway.mgl03",
            "--host", "192.168.1.100", "--token-file", "gateway.token"]);
        Assert.Equal("gateway.key", options.KeyFile);
        Assert.Equal("gateway.token", options.TokenFile);
        Assert.Equal("192.168.1.100", options.Target);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("setup")]
    [InlineData("download-agent")]
    public void Existing_Commands_Keep_Their_Original_Arguments(string command)
    {
        var options = SetupOptions.Parse([command, command == "download-agent" ? "--output" : "--host",
            command == "download-agent" ? "agent.bin" : "127.0.0.1", "--model", "lumi.gateway.mgl03"]);
        Assert.Equal(command, options.Command);
        Assert.Null(options.TokenFile);
        Assert.Null(options.KeyFile);
        Assert.Null(options.Token);
        Assert.Null(options.Key);
    }

    public static IEnumerable<object[]> InvalidOptions => new[]
    {
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token-file" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--unknown", "x" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--host", "127.0.0.2" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--key-file", "x" },
        new[] { "check", "--host", "127.0.0.1", "--key-file", "x" },
        new[] { "check", "--host", "127.0.0.1", "--key", "abcdefghijklmnop" },
        new[] { "check", "--host", "127.0.0.1", "--token", "x", "--key", "abcdefghijklmnop", "--key-file", "gateway.key" },
        new[] { "check", "--host", "127.0.0.1", "--token", "x", "--key", "abcdefghijklmnop", "--key", "ponmlkjihgfedcba" },
        new[] { "download-agent", "--output", "agent.bin", "--model", "lumi.gateway.mgl03", "--token", "x" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token", "x", "--token-file", "gateway.token" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token", "x", "--token", "y" },
        new[] { "setup", "--host", "invalid", "--model", "lumi.gateway.mgl03" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "unknown" },
        new[] { "setup", "--host", "127.0.0.1", "--model", "lumi.gateway.mgl03", "--token-file", " " }
    }.Select(args => new object[] { args });

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Invalid_Options_Are_Rejected_Before_Network_Operations(string[] args) =>
        Assert.Throws<ArgumentException>(() => SetupOptions.Parse(args));

    [Theory]
    [InlineData("1.5")]
    [InlineData("1.5.4beta")]
    [InlineData("1.5.4_12_extra")]
    [InlineData("-1.5.4")]
    [InlineData("1.5.4\n")]
    [InlineData("1.5.999999999999999999999")]
    public void Malformed_Firmware_Is_Rejected(string value) =>
        Assert.Throws<InvalidDataException>(() => GatewayFirmware.Parse(value));

    [Fact]
    public void Firmware_Comparison_Uses_Numbers_And_Builds()
    {
        Assert.True(GatewayFirmware.Parse("1.5.10").CompareTo(GatewayFirmware.Parse("1.5.9")) > 0);
        Assert.True(GatewayFirmware.Parse("1.4.6_10000").CompareTo(GatewayFirmware.Parse("1.4.6_0043")) > 0);
        Assert.Equal(0, GatewayFirmware.Parse("1.5.4_0000").CompareTo(GatewayFirmware.Parse("1.5.4")));
    }
}
