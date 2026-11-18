using MiHomeLib.Protocols;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraDualWallSwitchTests : Gateway2DeviceTestBase
{
    private const string Sid = "158d0001a1b2c3";
    private readonly AqaraDualWallSwitch _switch;
    private readonly Moq.Mock<MiHomeLib.XiaomiGateway2.IGateway2Writer> _writer = new();

    public AqaraDualWallSwitchTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, AqaraDualWallSwitch.XIAOMI_GATEWAY2_MODEL, Sid, 1234, _loggerFactory, out var device));
        _switch = Assert.IsType<AqaraDualWallSwitch>(device);
    }

    [Fact]
    public void Channels_Start_Idle()
    {
        _switch.StatusLeft.Should().Be("idle");
        _switch.StatusRight.Should().Be("idle");
    }

    [Fact]
    public void Left_Channel_Report_Updates_State_And_Raises_Event()
    {
        string raised = null;
        _switch.OnSwitchChannelLeftAsync += x => { raised = x; return Task.CompletedTask; };

        _switch.ParseData("{\"channel_0\":\"on\"}");

        raised.Should().Be("on");
        _switch.StatusLeft.Should().Be("on");
        _switch.StatusRight.Should().Be("idle");
    }

    [Fact]
    public void Right_Channel_Report_Updates_State_And_Raises_Event()
    {
        string raised = null;
        _switch.OnSwitchChannelRightAsync += x => { raised = x; return Task.CompletedTask; };

        _switch.ParseData("{\"channel_1\":\"off\"}");

        raised.Should().Be("off");
        _switch.StatusRight.Should().Be("off");
        _switch.ToString().Should().Be($"Device: QBKG03LM ctrl_neutral2 {Sid}, Left: idle, Right: off");
    }

    [Theory]
    [InlineData(true, true, "channel_0", "on")]
    [InlineData(false, false, "channel_1", "off")]
    public void Channel_Commands_Are_Sent_To_Gateway2(bool left, bool on, string channel, string status)
    {
        ((IProtocolMappedGateway2ManageableDevice)_switch).SetGateway2Writer(_writer.Object);

        if (left) { if (on) _switch.ChannelLeftPowerOn(); else _switch.ChannelLeftPowerOff(); }
        else { if (on) _switch.ChannelRightPowerOn(); else _switch.ChannelRightPowerOff(); }

        _writer.Verify(x => x.WriteAsync(Sid, "ctrl_neutral2",
            Moq.It.Is<(string Key, string Value)[]>(d => d.Length == 1 && d[0].Key == channel && d[0].Value == status)), Moq.Times.Once());
    }

    [Fact]
    public void Mqtt_Only_Settings_Are_Not_Supported_On_Gateway2()
    {
        ((IProtocolMappedGateway2ManageableDevice)_switch).SetGateway2Writer(_writer.Object);

        _switch.Invoking(x => x.SetLed(true)).Should().Throw<System.NotSupportedException>();
    }
    [Theory]
    [InlineData(true, true, "channel_0", "on")]
    [InlineData(false, false, "channel_1", "off")]
    public async Task Channel_Commands_Are_Sent_To_Gateway2_Async(bool left, bool on, string channel, string status)
    {
        ((IProtocolMappedGateway2ManageableDevice)_switch).SetGateway2Writer(_writer.Object);

        if (left) { if (on) await _switch.ChannelLeftPowerOnAsync(); else await _switch.ChannelLeftPowerOffAsync(); }
        else { if (on) await _switch.ChannelRightPowerOnAsync(); else await _switch.ChannelRightPowerOffAsync(); }

        _writer.Verify(x => x.WriteAsync(Sid, "ctrl_neutral2",
            Moq.It.Is<(string Key, string Value)[]>(d => d.Length == 1 && d[0].Key == channel && d[0].Value == status)), Moq.Times.Once());
    }
}
