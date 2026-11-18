using AutoFixture;
using FluentAssertions;
using MiHomeLib.Devices;
using System.Threading.Tasks;
using Xunit;
using static MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraWirelessMiniSwitchCN2Tests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("[{\"siid\":2,\"eiid\":1,\"arguments\":[]}]", ClickArg.SingleClick)]
    [InlineData("[{\"siid\":2,\"eiid\":2,\"arguments\":[]}]", ClickArg.DoubleClick)]
    [InlineData("[{\"siid\":2,\"eiid\":3,\"arguments\":[]}]", ClickArg.LongPressClick)]
    public void Check_Switch_OnClick_Event(string data, ClickArg evt)
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        var eventRaised = false;

        sw.OnClickAsync += clickArgs =>
        {
            eventRaised = clickArgs == evt;
            return Task.CompletedTask;
        };

        sw.ParseData(data);

        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"siid\":3,\"piid\":2,\"value\":3118}]", 3.118f)]
    public void Check_BatteryVoltage_Event(string data, float expected)
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        var eventRaised = false;
        float voltage = 0;

        sw.OnVoltageChangeAsync += arg =>
        {
            arg.Should().Be(0);
            voltage = sw.Voltage;
            eventRaised = true;
            return Task.CompletedTask;
        };

        sw.ParseData(data);

        voltage.Should().Be(expected);
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"siid\":3,\"piid\":1,\"value\":1}]", BatteryStatusType.Normal)]
    [InlineData("[{\"siid\":3,\"piid\":1,\"value\":2}]", BatteryStatusType.LowBattery)]
    public void Check_BatteryStatus_Event(string data, BatteryStatusType expected)
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        var eventRaised = false;
        BatteryStatusType? status = null;

        sw.OnBatteryStatusAsync += arg =>
        {
            sw.BatteryStatus.Should().Be(arg);
            status = arg;
            eventRaised = true;
            return Task.CompletedTask;
        };

        sw.ParseData(data);

        status.Value.Should().Be(expected);
        eventRaised.Should().BeTrue();
    }
    [Fact]
    public void Voltage_Reports_Pass_Previous_Value_And_Keep_Repeated_Reports()
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        var previous = new System.Collections.Generic.List<float>();
        sw.OnVoltageChangeAsync += old => { previous.Add(old); return Task.CompletedTask; };
        foreach (var value in new[] { 3000, 2850, 2850 })
            sw.ParseData(System.Text.Json.JsonSerializer.Serialize(new { siid = 3, piid = 2, value }));
        previous.Should().Equal(0, 3, 2.85f);
        sw.Voltage.Should().Be(2.85f);
        sw.GetInitialPropertyNames().Should().BeEmpty();
    }

    [Fact]
    public void Unknown_Event_Does_Not_Block_Following_Clicks()
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        var clicks = new System.Collections.Generic.List<ClickArg>();
        sw.OnClickAsync += click => { clicks.Add(click); return Task.CompletedTask; };
        foreach (var eiid in new[] { 99, 1, 1, 3 })
            sw.ParseData(System.Text.Json.JsonSerializer.Serialize(new { siid = 2, eiid }));
        clicks.Should().Equal(ClickArg.SingleClick, ClickArg.SingleClick, ClickArg.LongPressClick);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Unknown_Battery_Status_Is_Explicit(int value)
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        sw.BatteryStatus.Should().Be(BatteryStatusType.Unknown);
        sw.ParseData(System.Text.Json.JsonSerializer.Serialize(new { siid = 3, piid = 1, value }));
        sw.BatteryStatus.Should().Be(BatteryStatusType.Unknown);
    }
}
