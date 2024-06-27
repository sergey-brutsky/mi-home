using AutoFixture;
using MiHomeLib.DevicesV3;
using Xunit;
using FluentAssertions;
using static MiHomeLib.DevicesV3.AqaraOppleWirelesSwitch;

namespace MiHomeUnitTests.DevicesV3;

public class AqaraOppleTwoButtonsWirelesSwitchTests: MiHome3DeviceTests
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

        sw.OnButton1Click += (clickArgs) => { eventRaised = clickArgs == evt; };

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

        sw.OnButton2Click += (clickArgs) => { eventRaised = clickArgs == evt; };

        // Act
        sw.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    } 
}
