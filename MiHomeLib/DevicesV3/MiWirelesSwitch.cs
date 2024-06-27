using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

/// <summary>
/// WXKG01LM lumi.sensor_switch wireless switch
/// </summary>
public class MiWirelesSwitch: ZigBeeBatteryDevice
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
    public event Action<ClickArg> OnClick;
    private readonly IEnumerable<int> _validClickValues = Helpers.EnumToIntegers<ClickArg>();
    public MiWirelesSwitch(string did, ILoggerFactory loggerFactory): base(did, loggerFactory)
    {
        ResNamesToActions.Add(CLICK_RES_NAME, x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) OnClick?.Invoke((ClickArg)val);
        });
    }
    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) +  base.ToString();
}
