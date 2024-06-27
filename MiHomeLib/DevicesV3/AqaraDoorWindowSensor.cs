using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

// This sensor works exactly as XiaomiDoorWindowSensor, no need to repeat all stuff here
public class AqaraDoorWindowSensor(string did, ILoggerFactory loggerFactory) 
    : XiaomiDoorWindowSensor(did, loggerFactory)
{
    public new const string MARKET_MODEL = "MCCGQ11LM";
    public new const string MODEL = "lumi.sensor_magnet.aq2";
    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"Contact: {Contact}, " + GetBaseToString();
}
