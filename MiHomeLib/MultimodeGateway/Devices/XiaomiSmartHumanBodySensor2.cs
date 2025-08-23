using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

public class XiaomiSmartHumanBodySensor2 : BleBatteryDevice
{
    public const string MARKET_MODEL = "RTCGQ02LM";
    public const string MODEL = "lumi.motion.bmgl01";
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
    public const int PDID = 2701;
    private const int MOTION_EID = 15;
    private const int LIGHT_EID = 4120;
    private const int IDLE_TIME_EID = 4119;
    public MotionState Motion { get; set; } = MotionState.Unknown;
    public event Func<MotionState, Task> OnMotionDetectedAsync = (_) => Task.CompletedTask;
    public LightState Light { get; set; } = LightState.Unknown;
    public event Func<LightState, Task> OnLightChangeAsync = (_) => Task.CompletedTask;
    public event Func<NoMotionState, Task> OnNoMotionDetectedAsync = (_) => Task.CompletedTask;
    public XiaomiSmartHumanBodySensor2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(MOTION_EID, async x => 
        {
            var value = (MotionState)int.Parse(x);

            if(value == Motion) return; // prevent event duplication when state is actual

            (var oldValue, Motion) = (Motion, value);
            await OnMotionDetectedAsync(oldValue);
        });

        EidToActions.Add(LIGHT_EID, async x => 
        {
            var value = (LightState)int.Parse(x);

            if(value == Light) return; // prevent event duplication when state is actual

            (var oldValue, Light) = (Light, value);
            await OnLightChangeAsync(oldValue);
        });

        EidToActions.Add(IDLE_TIME_EID, async x => 
        {
            await OnNoMotionDetectedAsync((NoMotionState)ToBleInt256(x));
        });
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Motion: {Motion}, Light: {Light}";
    }
}
