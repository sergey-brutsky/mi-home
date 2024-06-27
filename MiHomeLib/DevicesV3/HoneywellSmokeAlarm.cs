using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public class HoneywellSmokeAlarm : BleBatteryDevice
{
    public enum SmokeState
    {
        Unknown = -1,
        SmokeDetected = 1,
        NoSmokeDetected = 0,
    }
    public const string MARKET_MODEL = "JTYJ-GD-03MI";
    public const string MODEL = "lumi.sensor_smoke.mcn02";
    public const int PDID = 2455;
    private const int SMOKE_DETECTED_EID = 4117;
    public SmokeState Smoke { get; set; } = SmokeState.Unknown;
    /// <summary>
    /// Old value of smoke passed as a parameter
    /// </summary>
    public event Action<SmokeState> OnSmokeChange;

    public HoneywellSmokeAlarm(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(SMOKE_DETECTED_EID, x => 
        {
            var val = (SmokeState)int.Parse(x);

            if(Smoke == val) return; // prevent event duplication if state is actual

            (var oldValue, Smoke) = (Smoke, val);
            
            OnSmokeChange?.Invoke(oldValue);
        });
    }

    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) 
            + $"Smoke state: {Smoke}, " + base.ToString();
        ;
    }
}
