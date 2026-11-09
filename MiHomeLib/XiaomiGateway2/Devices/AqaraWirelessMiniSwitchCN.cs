using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

/// <summary>
/// WXKG11LM lumi.remote.b1acn01 wireless mini switch
/// </summary>
public class AqaraWirelessMiniSwitchCN: BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "WXKG11LM";
    public const string MODEL = "remote.b1acn01";

    public AqaraWirelessMiniSwitchCN(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", x => 
        {
            Status = x.GetString();
         
            if (Status == "click")
            {
                RaiseEvent(() => OnClickAsync());
            } 
            else if (Status == "double_click")
            {
                RaiseEvent(() => OnDoubleClickAsync());
            }
            else if (Status == "long_click_press")
            {
                RaiseEvent(() => OnLongPressAsync());
            }
            else if (Status == "long_click_release")
            {
                RaiseEvent(() => OnLongReleaseAsync());
            }
        });
    }

    public event Func<Task> OnClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnDoubleClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnLongPressAsync = () => Task.CompletedTask;

    public event Func<Task> OnLongReleaseAsync = () => Task.CompletedTask;

    public string Status { get; private set; }    
}
