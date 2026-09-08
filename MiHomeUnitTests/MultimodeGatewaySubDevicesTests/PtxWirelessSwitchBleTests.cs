using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class PtxWirelessSwitchBleTests : MqttGatewayDeviceTests
{
    private readonly PtxWirelessSwitchBle _switch;

    public PtxWirelessSwitchBleTests()
    {
        _switch = _fixture.Build<PtxWirelessSwitchBle>().Create();
    }

    [Theory]
    [InlineData(4097, "01", PtxWirelessSwitchBle.ClickArg.SingleClick)]
    [InlineData(4097, "02", PtxWirelessSwitchBle.ClickArg.DoubleClick)]
    [InlineData(4097, "03", PtxWirelessSwitchBle.ClickArg.LongPress)]
    public void Check_OnClick_Event(int eid, string edata, PtxWirelessSwitchBle.ClickArg expectedClick)
    {
        // Arrange       
        var eventRaised = false;

        _switch.OnClickAsync += (clickArg) =>
        {
            eventRaised = true;
            clickArg.Should().Be(expectedClick);
            return Task.CompletedTask;
        };

        // Act
        _switch.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_OnClick_Event_Not_Raised_For_Invalid_Value()
    {
        // Arrange       
        var eventRaised = false;

        _switch.OnClickAsync += (_) =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act - use an invalid click value (99)
        _switch.ParseData(SetupBleAsyncEventParams(4097, "63").ToString());

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void Check_ToString()
    {
        // Act
        var result = _switch.ToString();

        // Assert
        result.Should().Contain(PtxWirelessSwitchBle.MARKET_MODEL);
        result.Should().Contain(PtxWirelessSwitchBle.MODEL);
    }
}