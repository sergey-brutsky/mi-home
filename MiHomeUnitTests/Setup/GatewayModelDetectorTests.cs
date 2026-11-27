using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.MiioDevices.Internal;
using MiHomeSetup;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class GatewayModelDetectorTests
{
    [Theory]
    [InlineData("rlxlinux login: ", "admin", "mips")]
    [InlineData("Aqara-Hub-E1 login: ", "root", "arm")]
    [InlineData("Aqara_Hub_E1 login: ", "root", "arm")]
    [InlineData("Mijia_Hub_V2 login: ", "root", "arm")]
    public void Known_Telnet_Banners_Select_Login_And_Architecture(string banner, string login, string architecture)
    {
        var profile = GatewayProfile.FromTelnetBanner(banner);
        Assert.Equal(login, profile.Login);
        Assert.Equal(architecture, profile.Architecture);
        Assert.Equal("# ", profile.Prompt);
    }

    [Fact]
    public void Unknown_Telnet_Banner_Does_Not_Guess_A_Model() =>
        Assert.Throws<InvalidDataException>(() => GatewayProfile.FromTelnetBanner("unknown login: "));

    [Theory]
    [InlineData("mips", "lumi.gateway.mgl03\r\n", "lumi.gateway.mgl03")]
    [InlineData("arm", "[persist.sys.model]: [lumi.gateway.mcn001]\r\n", "lumi.gateway.mcn001")]
    [InlineData("arm", "[persist.sys.model]: [lumi.gateway.mgl001]\n", "lumi.gateway.mgl001")]
    [InlineData("arm", "[persist.sys.model]: [lumi.gateway.aqcn02]", "lumi.gateway.aqcn02")]
    public void Shell_Output_Provides_Actual_Model_Including_CN_And_Global(string architecture, string output, string expected) =>
        Assert.Equal(expected, GatewayModelDetector.ParseShellModel(output, architecture));

    [Theory]
    [InlineData("mips", "")]
    [InlineData("mips", "lumi.gateway.mgl03\nlumi.gateway.mgl001")]
    [InlineData("arm", "[ro.product.model]: [lumi.gateway.mgl001]")]
    [InlineData("arm", "[persist.sys.model]: [lumi.gateway.mcn001]\n[persist.other.model]: [lumi.gateway.mgl001]")]
    public void Missing_Or_Ambiguous_Shell_Model_Is_Rejected(string architecture, string output) =>
        Assert.Throws<InvalidDataException>(() => GatewayModelDetector.ParseShellModel(output, architecture));

    [Theory]
    [InlineData("lumi.gateway.mgl03")]
    [InlineData("lumi.gateway.mcn001")]
    [InlineData("lumi.gateway.mgl001")]
    [InlineData("lumi.gateway.aqcn02")]
    [InlineData("lumi.gateway.v3")]
    public async Task Miio_Detection_Uses_Only_The_Read_Only_Info_Request(string model)
    {
        var transport = new Mock<IMiioRequests>(MockBehavior.Strict);
        transport.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"result\":{\"model\":\"" + model + "\"}}");
        Assert.Equal(model, (await GatewayModelDetector.ReadMiioInfoAsync(transport.Object, CancellationToken.None)).Model);
        transport.Verify(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()), Times.Once);
        transport.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Firmware_Is_Retained_For_Setup_Advice()
    {
        var transport = new Mock<IMiioRequests>(MockBehavior.Strict);
        transport.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"result\":{\"model\":\"lumi.gateway.mgl03\",\"fw_ver\":\"1.5.4_0090\"}}");
        var info = await GatewayModelDetector.ReadMiioInfoAsync(transport.Object, CancellationToken.None);
        Assert.Equal("1.5.4_0090", info.Firmware);
    }

    [Fact]
    public async Task Gateway2_Mac_Is_Retained_To_Scope_Multicast_Discovery()
    {
        var transport = new Mock<IMiioRequests>(MockBehavior.Strict);
        transport.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"result\":{\"model\":\"lumi.gateway.v3\",\"mac\":\"34:CE:00:12:34:56\"}}");
        var info = await GatewayModelDetector.ReadMiioInfoAsync(transport.Object, CancellationToken.None);
        Assert.Equal("34ce00123456", Gateway2Checker.SidFromMac(info.Mac));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"result\":null}")]
    [InlineData("{\"result\":{\"model\":3}}")]
    [InlineData("{\"result\":{}}")]
    public async Task Incomplete_Miio_Info_Is_Rejected(string response)
    {
        var transport = new Mock<IMiioRequests>(MockBehavior.Strict);
        transport.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => GatewayModelDetector.ReadMiioInfoAsync(transport.Object, CancellationToken.None));
    }

    [Fact]
    public async Task Unsupported_Miio_Model_Is_Rejected()
    {
        var transport = new Mock<IMiioRequests>(MockBehavior.Strict);
        transport.Setup(x => x.RequestAsync("miIO.info", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"result\":{\"model\":\"unknown\"}}");
        await Assert.ThrowsAsync<ArgumentException>(() => GatewayModelDetector.ReadMiioInfoAsync(transport.Object, CancellationToken.None));
    }
}
