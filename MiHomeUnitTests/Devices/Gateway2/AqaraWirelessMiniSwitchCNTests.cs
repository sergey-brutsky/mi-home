using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Xunit;
using static MiHomeLib.Devices.AqaraWirelessMiniSwitchCN;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraWirelessMiniSwitchCNTests : Gateway2DeviceTestBase
{
    private readonly string _sid = "158d0001a2b3c4";
    private readonly int _shortId = 22123;
    private readonly AqaraWirelessMiniSwitchCN _switch;

    public AqaraWirelessMiniSwitchCNTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL, _sid, _shortId, _loggerFactory, out var device));
        _switch = Assert.IsType<AqaraWirelessMiniSwitchCN>(device);
    }

    private void Parse(Dictionary<string, object> data) =>
        _switch.ParseData(Gateway2Message.Parse(CreateCommand("report", XIAOMI_GATEWAY2_MODEL, _sid, _shortId, data)).Data);

    [Theory]
    [InlineData("click", ClickArg.SingleClick)]
    [InlineData("double_click", ClickArg.DoubleClick)]
    [InlineData("long_click_press", ClickArg.LongPressHold)]
    [InlineData("long_click_release", ClickArg.LongPressRelease)]
    public void Status_Raises_OnClick_With_Matching_Argument(string status, ClickArg expected)
    {
        var raised = new List<ClickArg>();
        _switch.OnClickAsync += x => { raised.Add(x); return Task.CompletedTask; };

        Parse(new() { { "status", status } });

        raised.Should().Equal(expected);
        _switch.Status.Should().Be(status);
    }

    [Fact]
    public void Heartbeat_Sets_Voltage()
    {
        Parse(new() { { "voltage", 3025 } });

        _switch.Voltage.Should().Be(3.025f);
        _switch.ToString().Should().Be($"Device: WXKG11LM remote.b1acn01 {_sid}, Voltage: {3.025f}V, Status: ");
    }
}
