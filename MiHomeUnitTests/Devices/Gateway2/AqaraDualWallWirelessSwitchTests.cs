using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Xunit;
using static MiHomeLib.Devices.AqaraDualWallWirelessSwitch;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraDualWallWirelessSwitchTests : Gateway2DeviceTestBase
{
    private const string Sid = "158d0001b2c3d4";
    private readonly AqaraDualWallWirelessSwitch _switch;

    public AqaraDualWallWirelessSwitchTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL, Sid, 1234, _loggerFactory, out var device));
        _switch = Assert.IsType<AqaraDualWallWirelessSwitch>(device);
    }

    [Theory]
    [InlineData("channel_0", "click", Button.Left, ClickArg.SingleClick)]
    [InlineData("channel_0", "double_click", Button.Left, ClickArg.DoubleClick)]
    [InlineData("channel_0", "long_click", Button.Left, ClickArg.LongPressHold)]
    [InlineData("channel_1", "click", Button.Right, ClickArg.SingleClick)]
    [InlineData("channel_1", "double_click", Button.Right, ClickArg.DoubleClick)]
    [InlineData("channel_1", "long_click", Button.Right, ClickArg.LongPressHold)]
    [InlineData("dual_channel", "both_click", Button.Both, ClickArg.SingleClick)]
    public void Report_Raises_OnButtonClick(string channel, string action, Button button, ClickArg click)
    {
        var raised = new List<(Button, ClickArg)>();
        _switch.OnButtonClickAsync += (b, c) => { raised.Add((b, c)); return Task.CompletedTask; };

        _switch.ParseData($"{{\"{channel}\":\"{action}\"}}");

        raised.Should().Equal((button, click));
    }

    [Fact]
    public void Unknown_Action_Is_Logged()
    {
        var raised = false;
        _switch.OnButtonClickAsync += (_, _) => { raised = true; return Task.CompletedTask; };

        _switch.ParseData("{\"channel_0\":\"triple_click\"}");

        raised.Should().BeFalse();
        _loggerFactory.Entries.Should().Contain(x => x.Message.Contains("triple_click"));
    }

    [Fact]
    public void Voltage_And_ToString()
    {
        _switch.ParseData("{\"voltage\":3045}");

        _switch.Voltage.Should().Be(3.045f);
        _switch.ToString().Should().Be($"Device: WXKG02LM remote.b286acn01 {Sid}, Voltage: {3.045f}V");
    }
}
