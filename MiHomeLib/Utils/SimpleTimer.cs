using System;
using System.Timers;

namespace MiHomeLib.Utils;

public class SimpleTimer : ITimer
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
