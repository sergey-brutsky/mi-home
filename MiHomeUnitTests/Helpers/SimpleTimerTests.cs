using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices.Timing;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class SimpleTimerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2147483648)]
    public void Invalid_Interval_Is_Rejected(long milliseconds)
    {
        Action create = () => new SimpleTimer(TimeSpan.FromMilliseconds(milliseconds));
        create.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("interval");
    }

    [Fact]
    public void Null_Callback_Is_Rejected()
    {
        using var timer = new SimpleTimer(TimeSpan.FromSeconds(1));
        Action start = () => timer.Start(null);
        start.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("elapsed");
    }

    [Fact]
    public async Task Countdown_Fires_Once_And_Can_Be_Restarted_With_Another_Callback()
    {
        using var timer = new SimpleTimer(TimeSpan.FromMilliseconds(10));
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        timer.Start(() => { Interlocked.Increment(ref calls); first.TrySetResult(true); });
        await first.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);
        Volatile.Read(ref calls).Should().Be(1);

        timer.Start(() => second.TrySetResult(true));
        await second.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Volatile.Read(ref calls).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stopping_Or_Disposing_An_Active_Countdown_Prevents_Its_Callback(bool dispose)
    {
        using var timer = new SimpleTimer(TimeSpan.FromMilliseconds(200));
        var calls = 0;
        timer.Start(() => Interlocked.Increment(ref calls));

        if (dispose) timer.Dispose();
        else timer.Stop();

        // This observation window deliberately exceeds the original countdown.
        await Task.Delay(400);
        Volatile.Read(ref calls).Should().Be(0);
    }

    [Fact]
    public async Task Restarting_An_Active_Countdown_Replaces_The_Previous_Callback()
    {
        using var timer = new SimpleTimer(TimeSpan.FromMilliseconds(200));
        var oldCalls = 0;
        var newCalls = 0;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        timer.Start(() => Interlocked.Increment(ref oldCalls));

        timer.Start(() =>
        {
            Interlocked.Increment(ref newCalls);
            completed.TrySetResult(true);
        });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);
        Volatile.Read(ref oldCalls).Should().Be(0);
        Volatile.Read(ref newCalls).Should().Be(1);
    }

    [Fact]
    public void Disposed_Timer_Rejects_New_Countdowns_And_Repeated_Disposal_Is_Safe()
    {
        var timer = new SimpleTimer(TimeSpan.FromSeconds(1));
        timer.Dispose();
        timer.Dispose();
        timer.Stop();
        Action start = () => timer.Start(() => { });
        start.Should().Throw<ObjectDisposedException>();
    }
}
