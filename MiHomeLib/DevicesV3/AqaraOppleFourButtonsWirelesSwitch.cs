using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;
/// <summary>
/// WXCJKG12LM lumi.remote.b486opcn01 wireless switch
/// </summary>
public class AqaraOppleFourButtonsWirelesSwitch: AqaraOppleTwoButtonsWirelesSwitch
{
    public new const string MARKET_MODEL = "WXCJKG12LM";
    public new const string MODEL = "lumi.remote.b486opcn01";
    private const string BUTTON3_RES_NAME = "13.3.85";
    private const string BUTTON4_RES_NAME = "13.4.85"; 
    public event Action<ClickArg> OnButton3Click;
    public event Action<ClickArg> OnButton4Click;    
    public AqaraOppleFourButtonsWirelesSwitch(string did, ILoggerFactory loggerFactory): base(did, loggerFactory)
    {
        ResNamesToActions.Add(BUTTON3_RES_NAME, x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) OnButton3Click?.Invoke((ClickArg)val);
        });

        ResNamesToActions.Add(BUTTON4_RES_NAME, x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) OnButton4Click?.Invoke((ClickArg)val);
        });
    }

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + GetBaseToString();
}
