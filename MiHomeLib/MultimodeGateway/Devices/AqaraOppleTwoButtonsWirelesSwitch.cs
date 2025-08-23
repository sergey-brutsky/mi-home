using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

/// <summary>
/// WXCJKG11LM lumi.remote.b286opcn01 wireless switch
/// </summary>
public class AqaraOppleTwoButtonsWirelesSwitch: AqaraOppleWirelesSwitch
{
    public const string MARKET_MODEL = "WXCJKG11LM";
    public const string MODEL = "lumi.remote.b286opcn01";
    private const string BUTTON1_RES_NAME = "13.1.85";
    private const string BUTTON2_RES_NAME = "13.2.85"; 
    public event Func<ClickArg, Task> OnButton1ClickAsync = (_) => Task.CompletedTask;
    public event Func<ClickArg, Task> OnButton2ClickAsync = (_) => Task.CompletedTask;
    public AqaraOppleTwoButtonsWirelesSwitch(string did, ILoggerFactory loggerFactory): base(did, loggerFactory)
    {
        Actions.Add(BUTTON1_RES_NAME, async x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) await OnButton1ClickAsync((ClickArg)val);
        });

        Actions.Add(BUTTON2_RES_NAME, async x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) await OnButton2ClickAsync((ClickArg)val);
        });
    }

    protected string GetBaseToString() => base.ToString();
    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
}
