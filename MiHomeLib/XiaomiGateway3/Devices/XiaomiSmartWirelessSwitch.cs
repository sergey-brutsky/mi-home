using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;

/// <summary>
/// WXKG01LM lumi.sensor_switch wireless switch
/// </summary>
public class XiaomiSmartWirelessSwitch: ZigBeeBatteryDevice
{
    public const string MARKET_MODEL = "WXKG01LM";
    public const string MODEL = "lumi.sensor_switch";
    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        TripleClick = 3,
        QuadrupleClick = 4,
        /// <summary>
        /// More than 4 clicks
        /// </summary>
        ManyClicks = 128,
        LongPressHold = 16,
        LongPressRelease = 17,
    }    
    private const string CLICK_RES_NAME = "13.1.85"; 
    public event Func<ClickArg, Task> OnClickAsync = (_) => Task.CompletedTask;
    private readonly IEnumerable<int> _validClickValues = Helpers.EnumToIntegers<ClickArg>();
    public XiaomiSmartWirelessSwitch(string did, ILoggerFactory loggerFactory): base(did, loggerFactory)
    {
        Actions.Add(CLICK_RES_NAME, async x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) await OnClickAsync((ClickArg)val);
        });
    }
    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) +  base.ToString();
}
