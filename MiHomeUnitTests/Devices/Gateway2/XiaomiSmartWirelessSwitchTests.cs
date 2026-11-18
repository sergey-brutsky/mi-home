using MiHomeLib.Protocols;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using System.Threading.Tasks;
using Xunit;
using Switch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;

namespace MiHomeUnitTests.Devices.Gateway2;

public class XiaomiSmartWirelessSwitchTests : Gateway2DeviceTestBase
{
    [Theory]
    [InlineData("click", Switch.ClickArg.SingleClick)]
    [InlineData("double_click", Switch.ClickArg.DoubleClick)]
    [InlineData("long_click_press", Switch.ClickArg.LongPressHold)]
    [InlineData("long_click_release", Switch.ClickArg.LongPressRelease)]
    public void Check_Switch_Click_Status_Raises_Unified_Event(string status, Switch.ClickArg expected)
    {
        ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2,
            Switch.XIAOMI_GATEWAY2_MODEL,
            "34ce0067db36",
            33533,
            _loggerFactory,
            out var device).Should().BeTrue();
        var sensor = (Switch)device;

        Switch.ClickArg? actual = null;
        sensor.OnClickAsync += click =>
        {
            actual = click;
            return Task.CompletedTask;
        };

        sensor.ParseData($"{{\"status\":\"{status}\"}}");

        sensor.Status.Should().Be(status);
        actual.Should().Be(expected);
    }
}
