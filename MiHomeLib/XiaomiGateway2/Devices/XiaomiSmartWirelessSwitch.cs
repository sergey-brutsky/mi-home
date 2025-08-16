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
        });
    }

    public event Func<Task> OnClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnDoubleClickAsync = () => Task.CompletedTask;

    public event Func<Task> OnLongPressAsync = () => Task.CompletedTask;

    public string Status { get; private set; }    
}