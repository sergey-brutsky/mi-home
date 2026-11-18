using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;
using static MiHomeLib.Devices.AqaraDualWallWirelessSwitch;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraDualWallWirelessSwitchTests : ProtocolDeviceTestBase
{
    public static TheoryData<string> Models => new() { LUMI_MQTT_MODEL, LUMI_MQTT_MODEL_86SW2, LUMI_MQTT_MODEL_86SW2_ES1 };

    [Theory]
    [MemberData(nameof(Models))]
    public void All_Models_Are_Registered_And_Read_Battery(string model)
    {
        var sw = CreateLumiProtocolDevice<AqaraDualWallWirelessSwitch>(model);
        sw.GetInitialPropertyNames().Should().Equal("voltage", "battery");

        sw.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(85)]);

        sw.Voltage.Should().Be(3.005f);
        sw.BatteryPercent.Should().Be(85);
    }

    [Theory]
    [InlineData("13.1.85", 1, Button.Left, ClickArg.SingleClick)]
    [InlineData("13.1.85", 2, Button.Left, ClickArg.DoubleClick)]
    [InlineData("13.2.85", 16, Button.Right, ClickArg.LongPressHold)]
    [InlineData("13.2.85", 17, Button.Right, ClickArg.LongPressRelease)]
    [InlineData("13.5.85", 1, Button.Both, ClickArg.SingleClick)]
    public void Button_Reports_Raise_OnButtonClick(string resource, int code, Button button, ClickArg click)
    {
        var sw = CreateLumiProtocolDevice<AqaraDualWallWirelessSwitch>(LUMI_MQTT_MODEL);
        var raised = new List<(Button, ClickArg)>();
        sw.OnButtonClickAsync += (b, c) => { raised.Add((b, c)); return Task.CompletedTask; };

        sw.ParseData($"[{{\"res_name\":\"{resource}\",\"value\":{code}}}]");

        raised.Should().Equal((button, click));
    }

    [Fact]
    public void Diagnostic_Resources_Are_Taken()
    {
        var sw = CreateLumiProtocolDevice<AqaraDualWallWirelessSwitch>(LUMI_MQTT_MODEL);

        sw.ParseData("[{\"res_name\":\"8.0.9001\",\"value\":1},{\"res_name\":\"8.0.2006\",\"value\":27}]");

        sw.BatteryLow.Should().BeTrue();
        sw.ChipTemperature.Should().Be(27);
    }

    [Fact]
    public void Unknown_Click_Code_Is_Logged()
    {
        var sw = CreateLumiProtocolDevice<AqaraDualWallWirelessSwitch>(LUMI_MQTT_MODEL);

        sw.ParseData("[{\"res_name\":\"13.1.85\",\"value\":99}]");

        _loggerFactory.Entries.Should().Contain(x => x.Message.Contains("'99'"));
    }
}
