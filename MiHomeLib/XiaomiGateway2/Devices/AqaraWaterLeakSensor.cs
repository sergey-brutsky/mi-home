using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraWaterLeakSensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "SJCGQ11LM";
    public const string MODEL = "sensor_wleak.aq1";

    public AqaraWaterLeakSensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", async x => 
        {
            Status = x.GetString();

            if (Status == "leak")
            {
                await OnLeakAsync();
            }
            else if (Status == "no_leak")
            {
                await OnNoLeakAsync();
            }
        });
    }

    public event Func<Task> OnLeakAsync = () => Task.CompletedTask;
    public event Func<Task> OnNoLeakAsync = () => Task.CompletedTask;
    public string Status { get; private set; }
}