using System;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;

namespace MiHomeLib.XiaomiGateway3.Devices;
public class XiaomiSmartHumanBodySensor : ZigBeeBatteryDevice
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
    public event Func<Task> OnMotionDetectedAsync = () => Task.CompletedTask;
    public event Func<NoMotionInterval, Task> OnNoMotionDetectedAsync = (_) => Task.CompletedTask;
    public XiaomiSmartHumanBodySensor(string did, ILoggerFactory loggerFactory):
        this(
                did, 
                loggerFactory, 
                new SimpleTimer(TimeSpan.FromMinutes(1)), 
                new SimpleTimer(TimeSpan.FromMinutes(2))
        ) {}
    internal XiaomiSmartHumanBodySensor(string did, ILoggerFactory loggerFactory, ITimer oneMinuteTimer, ITimer twoMinutesTimer) 
        : base(did, loggerFactory)
    {
        _oneMinuteTimer = oneMinuteTimer;
        _twoMinutesTimer = twoMinutesTimer;

        _oneMinuteTimer.Elapsed += async (_, __) => 
        {
            await OnNoMotionDetectedAsync(NoMotionInterval.NoMotionForOneMinute);
            _oneMinuteTimer.Stop();
        };

        _twoMinutesTimer.Elapsed += async (_, __) => 
        {
            await OnNoMotionDetectedAsync(NoMotionInterval.NoMotionForTwoMinutes);
            _twoMinutesTimer.Stop();
        };

        Actions.Add(MOTION_RES_NAME, async x =>
        {
            _oneMinuteTimer.Stop();
            _twoMinutesTimer.Stop();
            await OnMotionDetectedAsync();
            _oneMinuteTimer.Start();
            _twoMinutesTimer.Start();
        });
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Voltage: {Voltage}, " + base.ToString();
    }

    internal class SimpleTimer : ITimer
    {
        private readonly Timer _timer = new();

        public SimpleTimer(TimeSpan timeSpan) => _timer.Interval = timeSpan.TotalMilliseconds;

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        public event ElapsedEventHandler Elapsed
        {
            add => _timer.Elapsed += value;
            remove => _timer.Elapsed -= value;
        }
    }
}
