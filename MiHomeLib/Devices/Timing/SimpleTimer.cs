using System;
using System.Timers;

namespace MiHomeLib.Devices.Timing;

/// <summary>
/// One-shot countdown used for MQTT motion and contact events.
/// </summary>
internal sealed class SimpleTimer : ITimer
{
    private readonly double _interval;
    private readonly object _timerLock = new();
    private Timer _timer;
    private bool _disposed;

    public SimpleTimer(TimeSpan interval)
    {
        _interval = interval.TotalMilliseconds;
        if (_interval <= 0 || _interval > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(interval));
    }

    public void Start(Action elapsed)
    {
        if (elapsed is null) throw new ArgumentNullException(nameof(elapsed));
        lock (_timerLock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SimpleTimer));
            _timer?.Dispose();
            var timer = new Timer(_interval) { AutoReset = false };
            timer.Elapsed += (_, _) => elapsed();
            _timer = timer;
            timer.Start();
        }
    }

    public void Stop()
    {
        lock (_timerLock) _timer?.Stop();
    }

    public void Dispose()
    {
        lock (_timerLock)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
