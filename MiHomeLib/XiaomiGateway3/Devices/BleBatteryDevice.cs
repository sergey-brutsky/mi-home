using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;

public abstract class BleBatteryDevice : BleDevice
{
    private const int BATTERY_EID = 4106;

    public BleBatteryDevice(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(BATTERY_EID, async x => 
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
