using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;
using static MiHomeLib.Devices.AqaraDualWallSwitch;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraDualWallSwitchTests : MqttDeviceTestBase
{
    [Fact]
    public void Disposing_From_Channel_Handler_Suppresses_Following_Button_Event()
    {
        using var sw = CreateSwitch();
        var clicks = 0;
        sw.OnSwitchChannelLeftAsync += _ => { sw.Dispose(); return Task.CompletedTask; };
        sw.OnButtonClickAsync += (_, _) => { clicks++; return Task.CompletedTask; };

        sw.ParseData("[{\"res_name\":\"4.1.85\",\"value\":1},{\"res_name\":\"13.1.85\",\"value\":1}]");

        clicks.Should().Be(0);
    }

    private AqaraDualWallSwitch CreateSwitch()
    {
        var sw = CreateLumiProtocolDevice<AqaraDualWallSwitch>(LUMI_MQTT_MODEL);
        ((IProtocolMappedManageableDevice)sw).SetMqttTransport(_mqttTransport.Object);
        return sw;
    }

    private string Write(string did, string resource, int value) =>
        $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"{resource}\",\"value\":{value}}}]}}";

    [Fact]
    public void Initial_Channels_Accept_Strings_And_Numbers()
    {
        var sw = CreateSwitch();
        sw.GetInitialPropertyNames().Should().Equal("neutral_0", "neutral_1");

        sw.SetInitialProperties([JsonValue.Create("on"), JsonValue.Create(0)]);

        sw.StatusLeft.Should().Be("on");
        sw.StatusRight.Should().Be("off");
    }

    [Fact]
    public void Channel_Reports_Update_State_And_Raise_Events()
    {
        var sw = CreateSwitch();
        string left = null, right = null;
        sw.OnSwitchChannelLeftAsync += x => { left = x; return Task.CompletedTask; };
        sw.OnSwitchChannelRightAsync += x => { right = x; return Task.CompletedTask; };

        sw.ParseData("[{\"res_name\":\"4.1.85\",\"value\":1},{\"res_name\":\"4.2.85\",\"value\":0}]");

        (left, right).Should().Be(("on", "off"));
        (sw.StatusLeft, sw.StatusRight).Should().Be(("on", "off"));
    }

    [Theory]
    [InlineData("13.1.85", 1, Button.Left, ClickArg.SingleClick)]
    [InlineData("13.2.85", 2, Button.Right, ClickArg.DoubleClick)]
    [InlineData("13.5.85", 1, Button.Both, ClickArg.SingleClick)]
    [InlineData("13.1.85", 16, Button.Left, ClickArg.LongPressHold)]
    [InlineData("13.2.85", 17, Button.Right, ClickArg.LongPressRelease)]
    public void Button_Reports_Raise_OnButtonClick(string resource, int code, Button button, ClickArg click)
    {
        var sw = CreateSwitch();
        var raised = new List<(Button, ClickArg)>();
        sw.OnButtonClickAsync += (b, c) => { raised.Add((b, c)); return Task.CompletedTask; };

        sw.ParseData($"[{{\"res_name\":\"{resource}\",\"value\":{code}}}]");

        raised.Should().Equal((button, click));
    }

    [Fact]
    public void Commands_Are_Written_To_Their_Resources()
    {
        var sw = CreateSwitch();

        sw.ChannelLeftPowerOn();
        sw.ChannelLeftPowerOff();
        sw.ChannelRightPowerOn();
        sw.ChannelRightPowerOff();
        sw.SetWirelessMode(Button.Left, true);
        sw.SetWirelessMode(Button.Right, false);
        sw.SetLed(true);

        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.1.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.1.85", 0)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.2.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.2.85", 0)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.10.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.11.85", 0)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "8.0.2032", 1)), Times.Once);
    }
    [Fact]
    public async Task Commands_Are_Written_To_Their_Resources_Async()
    {
        var sw = CreateSwitch();

        await sw.ChannelLeftPowerOnAsync();
        await sw.ChannelLeftPowerOffAsync();
        await sw.ChannelRightPowerOnAsync();
        await sw.ChannelRightPowerOffAsync();
        await sw.SetWirelessModeAsync(Button.Left, true);
        await sw.SetWirelessModeAsync(Button.Right, false);
        await sw.SetLedAsync(true);

        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.1.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.1.85", 0)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.2.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.2.85", 0)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.10.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "4.11.85", 0)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(Write(sw.DeviceId, "8.0.2032", 1)), Times.Once);
    }
}
