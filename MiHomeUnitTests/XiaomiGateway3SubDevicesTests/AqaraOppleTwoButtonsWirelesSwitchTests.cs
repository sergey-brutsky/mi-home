using AutoFixture;
using Xunit;
using FluentAssertions;
using static MiHomeLib.XiaomiGateway3.Devices.AqaraOppleWirelesSwitch;
using MiHomeLib.XiaomiGateway3.Devices;
using System.Threading.Tasks;

namespace MiHomeUnitTests.XiaomiGateway3SubDevices;

public class AqaraOppleTwoButtonsWirelesSwitchTests: Gw3DeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":1}]", ClickArg.SingleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":2}]", ClickArg.DoubleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":3}]", ClickArg.TripleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":16}]", ClickArg.LongPressHold)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":17}]", ClickArg.LongPressRelease)]
    public void Check_Switch_OnButton1Click_Event(string data, ClickArg evt)
    {
        // Arrange            
        var sw = _fixture.Create<AqaraOppleTwoButtonsWirelesSwitch>();
        var eventRaised = false;

        sw.OnButton1ClickAsync += (clickArgs) => 
        {
            eventRaised = clickArgs == evt;
            return Task.CompletedTask;
        };

        // Act
        sw.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"res_name\":\"13.2.85\",\"value\":1}]", ClickArg.SingleClick)]
    [InlineData("[{\"res_name\":\"13.2.85\",\"value\":2}]", ClickArg.DoubleClick)]
    [InlineData("[{\"res_name\":\"13.2.85\",\"value\":3}]", ClickArg.TripleClick)]
    [InlineData("[{\"res_name\":\"13.2.85\",\"value\":16}]", ClickArg.LongPressHold)]
    [InlineData("[{\"res_name\":\"13.2.85\",\"value\":17}]", ClickArg.LongPressRelease)]
    public void Check_Switch_OnButton2Click_Event(string data, ClickArg evt)
    {
        // Arrange            
        var sw = _fixture.Create<AqaraOppleTwoButtonsWirelesSwitch>();
        var eventRaised = false;

        sw.OnButton2ClickAsync += (clickArgs) => 
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
