using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiSmartHumanBodySensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "RTCGQ01LM";
    public const string MODEL = "motion";

    public XiaomiSmartHumanBodySensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", async x => 
        {
            await OnMotionAsync();
        });

        Actions.Add("no_motion", async x => 
        {
            await OnNoMotionAsync(int.Parse(x.GetString()));
        });
    }

    public event Func<Task> OnMotionAsync = () => Task.CompletedTask;
    /// <summary>
    /// No motion period in seconds passed as an argument, possible values 60s/120s/180s/300s/600s/1800s
    /// </summary>
    public event Func<int, Task> OnNoMotionAsync= (_) => Task.CompletedTask;  
}