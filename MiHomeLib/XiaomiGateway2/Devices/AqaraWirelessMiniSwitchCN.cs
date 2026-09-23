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
        Actions.Add("status", async x => 
        {
            Status = x.GetString();
         
            if (Status == "click")
            {
                await OnClickAsync();
            } 
            else if (Status == "double_click")
            {
                await OnDoubleClickAsync();
            }
            else if (Status == "long_click_press")
            {
                await OnLongPressAsync();
            }
            else if (Status == "long_click_release")
            {
                await OnLongReleaseAsync();
            }
        });
    }

    public event Func<Task> OnClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnDoubleClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnLongPressAsync = () => Task.CompletedTask;

    public event Func<Task> OnLongReleaseAsync = () => Task.CompletedTask;

    public string Status { get; private set; }    
}
