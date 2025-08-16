using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;
public class AqaraWaterLeakSensor : ZigBeeBatteryDevice
{
    public const string MARKET_MODEL = "SJCGQ11LM";
    public const string MODEL = "lumi.sensor_wleak.aq1";
    private const string MOISTURE_RES_NAME = "3.1.85";
    public bool Moisture { get; set; }
    public event Func<Task> OnMoistureChangeAsync = () => Task.CompletedTask;
    public AqaraWaterLeakSensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Actions.Add(MOISTURE_RES_NAME, async x =>
        {
            Moisture = x.GetInt32() == 1;
            await OnMoistureChangeAsync();
        });
    }

    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Moisture {Moisture}, " + base.ToString();
    }
}
