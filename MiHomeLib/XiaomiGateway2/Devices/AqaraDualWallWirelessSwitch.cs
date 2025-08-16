using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraDualWallWirelessSwitch: BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "WXKG02LM";
    public const string MODEL = "remote.b286acn01";
    private const string LeftChannel = "channel_0";
    private const string RightChannel = "channel_1";

    public AqaraDualWallWirelessSwitch(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add(LeftChannel, async x => 
        {
            var leftChannel = x.GetString();

            if (leftChannel == "click")
            {
                await OnLeftClickAsync();
            }
            else if (leftChannel == "double_click")
            {
                 await OnLeftDoubleClickAsync();
            }
            else if (leftChannel == "long_click")
            {
                await OnLeftLongClickAsync();
            }
        });

        Actions.Add(RightChannel, async x => 
        {
            var rightChannel = x.GetString();

            if (rightChannel == "click")
            {
                await OnRightClickAsync();
            }
            else if (rightChannel == "double_click")
            {
                await OnRightDoubleClickAsync();
            }
            else if (rightChannel == "long_click")
            {
                await OnRightLongClickAsync();
            }
        });
    }

    public event Func<Task> OnRightClickAsync = () => Task.CompletedTask;
    public event Func<Task> OnLeftClickAsync = () => Task.CompletedTask;
    public event Func<Task> OnRightDoubleClickAsync = () => Task.CompletedTask;
    public event Func<Task> OnLeftDoubleClickAsync = () => Task.CompletedTask;
    public event Func<Task> OnRightLongClickAsync = () => Task.CompletedTask;
    public event Func<Task> OnLeftLongClickAsync = () => Task.CompletedTask; 
}