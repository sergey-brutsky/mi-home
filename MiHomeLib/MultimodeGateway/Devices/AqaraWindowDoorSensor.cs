using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

// This sensor works exactly as XiaomiWindowDoorSensor, no need to repeat all stuff here
public class AqaraWindowDoorSensor(string did, ILoggerFactory loggerFactory) 
    : XiaomiWindowDoorSensor(did, loggerFactory)
{
    public new const string MARKET_MODEL = "MCCGQ11LM";
    public new const string MODEL = "lumi.sensor_magnet.aq2";
    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"Contact: {Contact}, " + GetBaseToString();
}
