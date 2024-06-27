using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public abstract class BleBatteryDevice : BleDevice
{
    private const int BATTERY_EID = 4106;

    public BleBatteryDevice(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(BATTERY_EID, x => 
        {
            var oldValue = BatteryPercent;
            BatteryPercent = x.ToBleByte();
            OnBatteryPercentChange?.Invoke(oldValue);
        });
    }

    public byte BatteryPercent { get; set; }
    /// <summary>
    /// Old value battery percent 0-100% (step 1%) passed as an argument
    /// </summary>
    public event Action<byte> OnBatteryPercentChange;

    public override string ToString() => $"BatteryPercent: {BatteryPercent}%";
}
