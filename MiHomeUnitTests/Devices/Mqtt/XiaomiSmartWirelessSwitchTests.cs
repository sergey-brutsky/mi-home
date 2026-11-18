using AutoFixture;
using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;
using static MiHomeLib.Devices.XiaomiSmartWirelessSwitch;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiSmartWirelessSwitchTests: ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":1}]", ClickArg.SingleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":2}]", ClickArg.DoubleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":3}]", ClickArg.TripleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":4}]", ClickArg.QuadrupleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":128}]", ClickArg.ManyClicks)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":16}]", ClickArg.LongPressHold)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":17}]", ClickArg.LongPressRelease)]
    public void Check_Switch_OnClick_Event(string data, ClickArg evt)
    {
        // Arrange            
        var sw = CreateLumiProtocolDevice<XiaomiSmartWirelessSwitch>(XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL);
        var eventRaised = false;

        sw.OnClickAsync += (clickArgs) => 
        {
             eventRaised = clickArgs == evt; 
             return Task.CompletedTask;
        };

        // Act
        sw.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }  
}
