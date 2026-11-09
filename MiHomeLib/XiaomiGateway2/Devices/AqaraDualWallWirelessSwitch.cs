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
        Actions.Add(LeftChannel, x => 
        {
            var leftChannel = x.GetString();

            if (leftChannel == "click")
            {
                RaiseEvent(() => OnLeftClickAsync());
            }
            else if (leftChannel == "double_click")
            {
                 RaiseEvent(() => OnLeftDoubleClickAsync());
            }
            else if (leftChannel == "long_click")
            {
                RaiseEvent(() => OnLeftLongClickAsync());
            }
        });

        Actions.Add(RightChannel, x => 
        {
            var rightChannel = x.GetString();

            if (rightChannel == "click")
            {
                RaiseEvent(() => OnRightClickAsync());
            }
            else if (rightChannel == "double_click")
            {
                RaiseEvent(() => OnRightDoubleClickAsync());
            }
            else if (rightChannel == "long_click")
            {
                RaiseEvent(() => OnRightLongClickAsync());
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