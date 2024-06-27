using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;
public class AqaraWaterLeakSensor : ZigBeeBatteryDevice
{
    public const string MARKET_MODEL = "SJCGQ11LM";
    public const string MODEL = "lumi.sensor_wleak.aq1";
    private const string MOISTURE_RES_NAME = "3.1.85";
    public bool Moisture { get; set; }
    public event Action OnMoistureChange;
    public AqaraWaterLeakSensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        ResNamesToActions.Add(MOISTURE_RES_NAME, x =>
        {
            Moisture = x.GetInt32() == 1;
            OnMoistureChange?.Invoke();
        });
    }

    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Moisture {Moisture}, " + base.ToString();
    }
}
