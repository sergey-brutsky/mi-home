using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;
/// <summary>
/// WXCJKG12LM lumi.remote.b486opcn01 wireless switch
/// </summary>
public class AqaraOppleFourButtonsWirelesSwitch: AqaraOppleTwoButtonsWirelesSwitch
{
    public new const string MARKET_MODEL = "WXCJKG12LM";
    public new const string MODEL = "lumi.remote.b486opcn01";
    private const string BUTTON3_RES_NAME = "13.3.85";
    private const string BUTTON4_RES_NAME = "13.4.85"; 
    public event Func<ClickArg, Task> OnButton3ClickAsync = (_) => Task.CompletedTask;
    public event Func<ClickArg, Task> OnButton4ClickAsync = (_) => Task.CompletedTask;
    public AqaraOppleFourButtonsWirelesSwitch(string did, ILoggerFactory loggerFactory): base(did, loggerFactory)
    {
        Actions.Add(BUTTON3_RES_NAME, async x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) await OnButton3ClickAsync((ClickArg)val);
        });

        Actions.Add(BUTTON4_RES_NAME, async x => 
        {
            var val = x.GetInt32();
            if(_validClickValues.Contains(val)) await OnButton4ClickAsync((ClickArg)val);
        });
    }

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + GetBaseToString();
}
