using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiSmartWirelessSwitchTests : Gw2DeviceTests
{
    private readonly string _sid = "34ce0067db36";
    private readonly int _shortId = 33533;
    private readonly XiaomiSmartWirelessSwitch _switch;

    public XiaomiSmartWirelessSwitchTests()
    {
        _switch = new XiaomiSmartWirelessSwitch(_sid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_Switch_Click_Raised()
    {
        // Arrange
        bool clickRaised = false;            
        
        _switch.OnClickAsync += () => 
        {
            clickRaised = true;
            return Task.CompletedTask;
        };

        // Act            
        var cmd = CreateCommand("report", XiaomiSmartWirelessSwitch.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "click" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.True(clickRaised);
    }

    [Fact]
    public void Check_Switch_DoubleClick_Raised()
    {
        // Arrange
        bool doubleClickRaised = false;

        _switch.OnDoubleClickAsync += () => 
        {
            doubleClickRaised = true;
            return Task.CompletedTask;
        };


        // Act
        var cmd = CreateCommand("report", XiaomiSmartWirelessSwitch.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "double_click" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.True(doubleClickRaised);
    }

    [Fact]
    public void Check_Switch_LongPressClick_Raised()
    {
        // Arrange
        bool longClickRaised = false;

        _switch.OnLongPressAsync += () => 
        {
            longClickRaised = true;
            return Task.CompletedTask;
        };

        // Act
        var cmd = CreateCommand("report", XiaomiSmartWirelessSwitch.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "long_click_press" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.True(longClickRaised);
    }
}
