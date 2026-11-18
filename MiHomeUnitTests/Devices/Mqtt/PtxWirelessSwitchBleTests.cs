using MiHomeLib.Protocols;
using System.Text.Json;
using Xunit;
using MiHomeLib;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;

namespace MiHomeUnitTests.Devices.Mqtt;

public class PtxWirelessSwitchBleTests : ProtocolDeviceTestBase
{
    private readonly PtxWirelessSwitchBle _switch;

    public PtxWirelessSwitchBleTests()
    {
        _switch = CreateLumiBleProtocolDevice<PtxWirelessSwitchBle>(PtxWirelessSwitchBle.PDID);
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

        // Any event of service 3 is interpreted as a low battery notification.
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
    [Fact]
    public void Repeated_Clicks_And_Array_Payloads_Are_Handled()
    {
        var clicks = new System.Collections.Generic.List<PtxWirelessSwitchBle.ClickArg>();
        _switch.OnClickAsync += click => { clicks.Add(click); return Task.CompletedTask; };
        _switch.ParseData("[{\"siid\":99,\"eiid\":1012},{\"siid\":2,\"eiid\":1012},{\"siid\":2,\"eiid\":1012}]");
        clicks.Should().Equal(PtxWirelessSwitchBle.ClickArg.SingleClick, PtxWirelessSwitchBle.ClickArg.SingleClick);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1012)]
    [InlineData(9999)]
    public void Low_Battery_Is_A_Service_Event_Without_Clicks(int eiid)
    {
        var low = 0;
        var clicks = 0;
        _switch.OnLowBatteryAsync += () => { low++; return Task.CompletedTask; };
        _switch.OnClickAsync += _ => { clicks++; return Task.CompletedTask; };
        _switch.ParseData(BuildParseData(3, eiid));
        _switch.ParseData(BuildParseData(3, eiid));
        low.Should().Be(2);
        clicks.Should().Be(0);
    }

    [Fact]
    public void Mac_Is_Assigned_By_Gateway_And_Included_In_Description()
    {
        ((IProtocolMappedBleDevice)_switch).SetMacAddress("aa:bb:cc:dd:ee:ff");
        _switch.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        _switch.ToString().Should().Contain(_switch.DeviceId).And.Contain(_switch.Mac);
    }
}
