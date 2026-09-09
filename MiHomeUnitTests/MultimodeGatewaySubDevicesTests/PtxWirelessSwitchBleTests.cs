using System.Text.Json;
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

    private static string BuildParseData(int siid, int eiid)
    {
        return JsonSerializer.Serialize(new { siid, eiid });
    }

    [Theory]
    [InlineData(2, 1012, PtxWirelessSwitchBle.ClickArg.SingleClick)]
    [InlineData(2, 1013, PtxWirelessSwitchBle.ClickArg.DoubleClick)]
    [InlineData(2, 1014, PtxWirelessSwitchBle.ClickArg.LongPressClick)]
    public void Check_OnClick_Event(int siid, int eiid, PtxWirelessSwitchBle.ClickArg expectedClick)
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
        _switch.ParseData(BuildParseData(siid, eiid));

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_OnClick_Event_Not_Raised_For_Invalid_Eiid()
    {
        // Arrange       
        var eventRaised = false;

        _switch.OnClickAsync += (_) =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act - use an invalid eiid value (9999)
        _switch.ParseData(BuildParseData(2, 9999));

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void Check_OnLowBattery_Event()
    {
        // Arrange       
        var eventRaised = false;

        _switch.OnLowBatteryAsync += () =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act - siid 3 is low battery, eiid must be a valid ClickArg for Enum.IsDefined check
        _switch.ParseData(BuildParseData(3, 1012));

        // Assert
        eventRaised.Should().BeTrue();
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