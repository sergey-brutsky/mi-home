using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class AqaraWirelessMiniSwitchCNTests : Gw2DeviceTests
{
    private readonly string _sid = "158d0001a2b3c4";
    private readonly int _shortId = 22123;
    private readonly AqaraWirelessMiniSwitchCN _switch;

    public AqaraWirelessMiniSwitchCNTests()
    {
        _switch = new AqaraWirelessMiniSwitchCN(_sid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_Switch_Click_Raised()
    {
        bool clickRaised = false;

        _switch.OnClickAsync += () =>
        {
            clickRaised = true;
            return Task.CompletedTask;
        };

        var cmd = CreateCommand("report", AqaraWirelessMiniSwitchCN.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "click" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        Assert.True(clickRaised);
    }

    [Fact]
    public void Check_Switch_DoubleClick_Raised()
    {
        bool doubleClickRaised = false;

        _switch.OnDoubleClickAsync += () =>
        {
            doubleClickRaised = true;
            return Task.CompletedTask;
        };

        var cmd = CreateCommand("report", AqaraWirelessMiniSwitchCN.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "double_click" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        Assert.True(doubleClickRaised);
    }

    [Fact]
    public void Check_Switch_LongPressClick_Raised()
    {
        bool longClickRaised = false;

        _switch.OnLongPressAsync += () =>
        {
            longClickRaised = true;
            return Task.CompletedTask;
        };

        var cmd = CreateCommand("report", AqaraWirelessMiniSwitchCN.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "long_click_press" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        Assert.True(longClickRaised);
    }

    [Fact]
    public void Check_Switch_LongReleaseClick_Raised()
    {
        bool longClickRaised = false;

        _switch.OnLongReleaseAsync += () =>
        {
            longClickRaised = true;
            return Task.CompletedTask;
        };

        var cmd = CreateCommand("report", AqaraWirelessMiniSwitchCN.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", "long_click_release" },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        Assert.True(longClickRaised);
    }
}
