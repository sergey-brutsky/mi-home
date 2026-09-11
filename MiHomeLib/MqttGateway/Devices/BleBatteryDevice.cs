using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class BleBatteryDevice : BleDevice
{
    protected const int BATTERY_EID = 4106;

    public BleBatteryDevice(string did, ILoggerFactory loggerFactory) : this(did, loggerFactory, BATTERY_EID)
    {
    }

    /// <summary>
    /// Devices talking mibeacon v2 report battery with their own eid (e.g. 18435 instead of 4106)
    /// </summary>
    protected BleBatteryDevice(string did, ILoggerFactory loggerFactory, int batteryEid) : base(did, loggerFactory)
    {
        EidToActions.Add(batteryEid, async x => 
        {
            var oldValue = BatteryPercent;
            BatteryPercent = ToBleByte(x);
            await OnBatteryPercentChangeAsync(oldValue);
        });
    }

    public byte BatteryPercent { get; set; }
    /// <summary>
    /// Old value battery percent 0-100% (step 1%) passed as an argument
    /// </summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = (_) => Task.CompletedTask;

    public override string ToString() => $"BatteryPercent: {BatteryPercent}%";
}
