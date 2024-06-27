using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public class XiaomiMotionSensor2 : BleBatteryDevice
{
    public enum MotionState
    {
        Unknown = -1,
        MotionWithoutLight = 0,
        MotionWithLight = 100,        
    }
    public enum NoMotionState
    {   // Only these intervals are exposed by the sensor 
        Idle120Seconds = 120,
        Idle300Seconds = 300,
    }
    public enum LightState
    {
        Unknown = -1,
        LightOff = 0,
        LightOn = 1,        
    }
    public const string MARKET_MODEL = "RTCGQ02LM";
    public const string MODEL = "lumi.motion.bmgl01";
    public const int PDID = 2701;
    private const int MOTION_EID = 15;
    private const int LIGHT_EID = 4120;
    private const int IDLE_TIME_EID = 4119;
    public MotionState Motion { get; set; } = MotionState.Unknown;
    public event Action<MotionState> OnMotionDetected;
    public LightState Light { get; set; } = LightState.Unknown;
    public event Action<LightState> OnLightChange;
    public event Action<NoMotionState> OnNoMotionDetected;
    public XiaomiMotionSensor2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(MOTION_EID, x => 
        {
            var value = (MotionState)int.Parse(x);

            if(value == Motion) return; // prevent event duplication when state is actual

            (var oldValue, Motion) = (Motion, value);
            OnMotionDetected?.Invoke(oldValue);
        });

        EidToActions.Add(LIGHT_EID, x => 
        {
            var value = (LightState)int.Parse(x);

            if(value == Light) return; // prevent event duplication when state is actual

            (var oldValue, Light) = (Light, value);
            OnLightChange?.Invoke(oldValue);
        });

        EidToActions.Add(IDLE_TIME_EID, x => 
        {
            OnNoMotionDetected?.Invoke((NoMotionState)x.ToBleInt256());
        });
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Motion: {Motion}, Light: {Light}";
    }
}
