using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;
using MiHomeLib.XiaomiGateway2;
using MiHomeSetup;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class Gateway2CheckerTests
{
    private const string Sid = "34ce00123456";
    private const string DeveloperKey = "abcdefghijklmnop";

    [Theory]
    [InlineData("34:CE:00:12:34:56")]
    [InlineData("34:ce:00:12:34:56")]
    public void Sid_Is_Normalized_From_The_Authenticated_Mac(string mac) =>
        Assert.Equal(Sid, Gateway2Checker.SidFromMac(mac));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("gg:gg:gg:gg:gg:gg")]
    public void Unknown_Mac_Cannot_Select_An_Arbitrary_Gateway(string mac) =>
        Assert.Throws<InvalidDataException>(() => Gateway2Checker.SidFromMac(mac));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Discovery_Checks_Readiness_And_Reports_Developer_Key_Without_Writing(bool keyAvailable)
    {
        var context = CreateGateway(keyAvailable: keyAvailable);
        using var gateway = context.Gateway;
        using var output = new StringWriter();
        Assert.True(await Gateway2Checker.CheckAsync(gateway, CancellationToken.None, output));
        Assert.Equal(Sid, gateway.Sid);
        Assert.Contains("PASS Gateway 2 UDP discovery (0 supported devices found)", output.ToString());
        Assert.Contains("READY (miIO and Gateway 2 UDP discovery)", output.ToString());
        Assert.Contains(keyAvailable ? "PASS Developer key is available" : "WARN Developer key is unavailable", output.ToString());
        Assert.DoesNotContain(DeveloperKey, output.ToString());
        context.Udp.Verify(x => x.SendWrite(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<(string Key, string Value)[]>()), Times.Never);
        context.Miio.Verify(x => x.SendMessageAsync(It.Is<string>(request =>
            request.Contains("get_lumi_dpf_aes_key"))), Times.Once);
        Assert.Single(context.Miio.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_Or_Other_Gateway_Reply_Does_Not_Pass_The_Check(bool otherGatewayReplies)
    {
        var context = CreateGateway(reply: otherGatewayReplies, replySid: "ffffffffffff");
        using var gateway = context.Gateway;
        using var output = new StringWriter();
        Assert.False(await Gateway2Checker.CheckAsync(gateway, CancellationToken.None, output));
        Assert.Null(gateway.Sid);
        Assert.Contains("UDP port 9898", output.ToString());
        Assert.Contains("NOT READY", output.ToString());
        Assert.Contains("Enable the Gateway 2 LAN API", output.ToString());
    }

    [Fact]
    public async Task Setup_Retries_Discovery_While_Lan_Access_Starts()
    {
        var context = CreateGateway(reply: false);
        using var gateway = context.Gateway;
        var attempts = 0;
        context.Udp.Setup(x => x.SendDiscover()).Callback(() =>
        {
            if (++attempts == 2)
                context.Udp.Raise(x => x.OnMessageReceived += null,
                    JsonSerializer.Serialize(new { cmd = "get_id_list_ack", sid = Sid, token = DeveloperKey, data = "[]" }));
        });
        using var output = new StringWriter();
        Assert.True(await Gateway2Checker.CheckAsync(gateway, CancellationToken.None, output,
            discoveryAttempts: 3, retryDelay: TimeSpan.Zero));
        Assert.Equal(2, attempts);
        Assert.Contains("Waiting for Gateway 2 UDP LAN access", output.ToString());
    }

    [Fact]
    public async Task Cancellation_Stops_Pending_Discovery()
    {
        var context = CreateGateway(reply: false);
        using var gateway = context.Gateway;
        using var cancellation = new CancellationTokenSource();
        var checking = Gateway2Checker.CheckAsync(gateway, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checking);
    }

    [Fact]
    public async Task Explicit_Gateway2_Check_Requires_A_Token_Before_Any_Network_Access()
    {
        var options = SetupOptions.Parse(["check", "--host", "127.0.0.1", "--model", Gateway2Checker.Model]);
        await Assert.ThrowsAsync<ArgumentException>(() => Gateway2Checker.RunAsync(options,
            new GatewayInfo(Gateway2Checker.Model, null), CancellationToken.None));
    }

    private static (XiaomiGateway2 Gateway, Mock<IMiioTransport> Miio, Mock<IGateway2Transport> Udp) CreateGateway(
        bool reply = true, bool keyAvailable = true, string replySid = Sid)
    {
        var miio = new Mock<IMiioTransport>();
        miio.Setup(x => x.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(keyAvailable
            ? JsonSerializer.Serialize(new { result = new[] { DeveloperKey } })
            : "{\"error\":{\"code\":-1}}");
        var udp = new Mock<IGateway2Transport>();
        if (reply)
            udp.Setup(x => x.SendDiscover()).Callback(() => udp.Raise(x => x.OnMessageReceived += null,
                JsonSerializer.Serialize(new { cmd = "get_id_list_ack", sid = replySid, token = DeveloperKey, data = "[]" })));
        udp.Setup(x => x.SendRead(Sid)).Callback(() => udp.Raise(x => x.OnMessageReceived += null,
            JsonSerializer.Serialize(new { cmd = "read_ack", sid = Sid, model = "gateway", data = "{}" })));
        var gateway = new XiaomiGateway2(miio.Object, udp.Object, Sid, loggerFactory: NullLoggerFactory.Instance)
        {
            DiscoveryTimeout = TimeSpan.FromMilliseconds(40),
            ReadAnswerTimeout = TimeSpan.FromMilliseconds(10),
            ReadDeviceInterval = TimeSpan.Zero,
            ReadAttempts = 1
        };
        return (gateway, miio, udp);
    }
}
