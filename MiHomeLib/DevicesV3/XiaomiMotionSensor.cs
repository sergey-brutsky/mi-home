using System;
using Microsoft.Extensions.Logging;
using MiHomeLib.Utils;

namespace MiHomeLib.DevicesV3;
public class XiaomiMotionSensor : ZigBeeBatteryDevice
{   
    public const string MARKET_MODEL = "RTCGQ01LM";
    public const string MODEL = "lumi.sensor_motion";
    // I use these exact intervals because only these events are sent to miio/report mqtt topic
    public enum NoMotionInterval
    {
        NoMotionForOneMinute,
        NoMotionForTwoMinutes,
    }
    private const string MOTION_RES_NAME = "3.1.85";
    private readonly ITimer _oneMinuteTimer;
    private readonly ITimer _twoMinutesTimer;
    public bool MotionDetected { get; set; }
    public event Action OnMotionDetected;
    public event Action<NoMotionInterval> OnNoMotionDetected;    
    public XiaomiMotionSensor(string did, ILoggerFactory loggerFactory):
        this(
                did, 
                loggerFactory, 
                new SimpleTimer(TimeSpan.FromMinutes(1)), 
                new SimpleTimer(TimeSpan.FromMinutes(2))
        ) {}
    internal XiaomiMotionSensor(string did, ILoggerFactory loggerFactory, ITimer oneMinuteTimer, ITimer twoMinutesTimer) 
        : base(did, loggerFactory)
    {
        _oneMinuteTimer = oneMinuteTimer;
        _twoMinutesTimer = twoMinutesTimer;

        _oneMinuteTimer.Elapsed += (_, __) => 
        {
            OnNoMotionDetected?.Invoke(NoMotionInterval.NoMotionForOneMinute);
            _oneMinuteTimer.Stop();
        };

        _twoMinutesTimer.Elapsed += (_, __) => 
        {
            OnNoMotionDetected?.Invoke(NoMotionInterval.NoMotionForTwoMinutes);
            _twoMinutesTimer.Stop();
        };

        ResNamesToActions.Add(MOTION_RES_NAME, x =>
        {
            _oneMinuteTimer.Stop();
            _twoMinutesTimer.Stop();
            MotionDetected = x.GetInt32() == 1;
            OnMotionDetected?.Invoke();
            _oneMinuteTimer.Start();
            _twoMinutesTimer.Start();
        });
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Motion: {MotionDetected}, " + base.ToString();
    }
}
