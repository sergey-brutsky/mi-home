using AutoFixture;
using FluentAssertions;
using MiHomeLib.MqttGateway.Devices;
using System.Threading.Tasks;
using Xunit;
using static MiHomeLib.MqttGateway.Devices.AqaraWirelessMiniSwitchCN2;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class AqaraWirelessMiniSwitchCN2Tests : MqttGatewayDeviceTests
{
    [Theory]
    [InlineData("[{\"siid\":2,\"eiid\":1,\"arguments\":[]}]", ClickArg.SingleClick)]
    [InlineData("[{\"siid\":2,\"eiid\":2,\"arguments\":[]}]", ClickArg.DoubleClick)]
    [InlineData("[{\"siid\":2,\"eiid\":3,\"arguments\":[]}]", ClickArg.LongPressClick)]
    public void Check_Switch_OnClick_Event(string data, ClickArg evt)
    {
        var sw = _fixture.Create<AqaraWirelessMiniSwitchCN2>();
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
        var sw = _fixture.Create<AqaraWirelessMiniSwitchCN2>();
        var eventRaised = false;
        float voltage = 0;

        sw.OnBatteryVoltageAsync += arg =>
        {
            voltage = arg;
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
        var sw = _fixture.Create<AqaraWirelessMiniSwitchCN2>();
        var eventRaised = false;
        BatteryStatusType? status = null;
        
        sw.OnBatteryStatusAsync += arg =>
        {
            status = arg;
            eventRaised = true;
            return Task.CompletedTask;
        };

        sw.ParseData(data);

        status.Value.Should().Be(expected);
        eventRaised.Should().BeTrue();        
    }
}
