using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;
using static MiHomeLib.Devices.AqaraWirelessMiniSwitchCN;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraWirelessMiniSwitchCNTests : ProtocolDeviceTestBase
{
    private AqaraWirelessMiniSwitchCN CreateSwitch() => CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN>(LUMI_MQTT_MODEL);

    [Theory]
    [InlineData(1, ClickArg.SingleClick)]
    [InlineData(2, ClickArg.DoubleClick)]
    [InlineData(16, ClickArg.LongPressHold)]
    [InlineData(17, ClickArg.LongPressRelease)]
    public void Check_Switch_OnClick_Event(int value, ClickArg expected)
    {
        var sw = CreateSwitch();
        ClickArg? raised = null;
        sw.OnClickAsync += x => { raised = x; return Task.CompletedTask; };

        sw.ParseData($"[{{\"res_name\":\"13.1.85\",\"value\":{value}}}]");

        raised.Should().Be(expected);
    }

    [Fact]
    public void Unknown_Click_Value_Is_Ignored()
    {
        var sw = CreateSwitch();
        var raised = false;
        sw.OnClickAsync += _ => { raised = true; return Task.CompletedTask; };

        sw.ParseData("[{\"res_name\":\"13.1.85\",\"value\":3}]");

        raised.Should().BeFalse();
    }

    [Fact]
    public void Initial_Properties_And_ToString()
    {
        var sw = CreateSwitch();
        sw.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");
        sw.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(95), JsonValue.Create(120), JsonValue.Create(25)]);

        sw.BatteryPercent.Should().Be(95);
        sw.ToString().Should().Contain(MARKET_MODEL).And.Contain(LUMI_MQTT_MODEL);
    }
}
