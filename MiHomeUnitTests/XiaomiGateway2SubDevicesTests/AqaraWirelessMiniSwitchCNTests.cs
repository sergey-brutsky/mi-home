using System;
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

    private static readonly Dictionary<string, Action<AqaraWirelessMiniSwitchCN, Func<Task>>> Subscribe = new()
    {
        ["click"] = (sw, h) => sw.OnClickAsync += h,
        ["double_click"] = (sw, h) => sw.OnDoubleClickAsync += h,
        ["long_click_press"] = (sw, h) => sw.OnLongPressAsync += h,
        ["long_click_release"] = (sw, h) => sw.OnLongReleaseAsync += h,
    };

    [Theory]
    [InlineData("click")]
    [InlineData("double_click")]
    [InlineData("long_click_press")]
    [InlineData("long_click_release")]
    public void Status_Raises_Matching_Event(string status)
    {
        var raised = false;
        Subscribe[status](_switch, () => { raised = true; return Task.CompletedTask; });

        var cmd = CreateCommand("report", AqaraWirelessMiniSwitchCN.MODEL, _sid, _shortId, new Dictionary<string, object>
            {
                { "status", status },
            });

        _switch.ParseData(ResponseCommand.FromString(cmd).Data);

        Assert.True(raised);
    }
}
