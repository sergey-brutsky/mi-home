using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

/// <summary>
/// WXCJKG11LM lumi.remote.b286opcn01 wireless switch
/// </summary>
public class AqaraOppleTwoButtonsWirelesSwitch: AqaraOppleWirelesSwitch
{
    public const string MARKET_MODEL = "WXCJKG11LM";
    public const string MODEL = "lumi.remote.b286opcn01";
    private const string BUTTON1_RES_NAME = "13.1.85";
    private const string BUTTON2_RES_NAME = "13.2.85"; 
    public event Action<ClickArg> OnButton1Click;
    public event Action<ClickArg> OnButton2Click;    
    public AqaraOppleTwoButtonsWirelesSwitch(string did, ILoggerFactory loggerFactory): base(did, loggerFactory)
    {
        ResNamesToActions.Add(BUTTON1_RES_NAME, x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) OnButton1Click?.Invoke((ClickArg)val);
        });

        ResNamesToActions.Add(BUTTON2_RES_NAME, x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) OnButton2Click?.Invoke((ClickArg)val);
        });
    }

    protected string GetBaseToString() => base.ToString();
    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
}
