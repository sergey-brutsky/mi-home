using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiSmartWirelessSwitch: BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "WXKG01LM";
    public const string MODEL = "switch";

    public XiaomiSmartWirelessSwitch(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
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
        });
    }

    public event Func<Task> OnClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnDoubleClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnLongPressAsync = () => Task.CompletedTask;

    public string Status { get; private set; }    
}