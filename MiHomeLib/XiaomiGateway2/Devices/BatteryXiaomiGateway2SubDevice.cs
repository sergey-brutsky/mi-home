using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public abstract class BatteryXiaomiGateway2SubDevice : XiaomiGateway2SubDevice
{
    public float Voltage { get; private set; }

    public BatteryXiaomiGateway2SubDevice(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("voltage", x => Voltage = x.GetInt32()/1000f);
    }
    public override string ToString() => base.ToString() + $", Voltage: {Voltage}V";    
}
