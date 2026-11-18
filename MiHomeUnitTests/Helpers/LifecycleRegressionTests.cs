using MiHomeUnitTests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading;
using ITimer = MiHomeLib.Devices.Timing.ITimer;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.Devices.Timing;
using MiHomeLib.Protocols;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class LifecycleRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Timer_Cleanup_Attempts_Every_Stop_And_Dispose_Despite_Failures(bool motion)
    {
        var timers = new List<Mock<ITimer>>();
        var callbacks = new List<Action>();
        for (var i = 0; i < (motion ? MotionSensorBase.NoMotionPeriods.Count : 2); i++)
        {
            var timer = new Mock<ITimer>();
            timer.Setup(value => value.Start(It.IsAny<Action>())).Callback<Action>(callbacks.Add);
            timers.Add(timer);
        }
        var index = 0;
        ProtocolMappedDeviceBase sensor = motion
            ? new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance, _ => timers[index++].Object)
            : new XiaomiWindowDoorSensor("contact", 0, NullLoggerFactory.Instance, timers[0].Object, timers[1].Object);
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        sensor.ParseData(Motion);
        var stopFailure = new InvalidOperationException("stop failed");
        var disposeFailure = new InvalidOperationException("dispose failed");
        foreach (var timer in timers) timer.Invocations.Clear();
        timers[0].Setup(timer => timer.Stop()).Throws(stopFailure);
        timers[^1].Setup(timer => timer.Dispose()).Throws(disposeFailure);
        Action dispose = sensor.Dispose;

        dispose.Should().ThrowExactly<AggregateException>().Which.Flatten().InnerExceptions
            .Should().Equal(stopFailure, disposeFailure);

        foreach (var timer in timers)
        {
            timer.Verify(value => value.Stop(), Times.Once);
            timer.Verify(value => value.Dispose(), Times.Once);
        }
        dispose.Should().NotThrow();
        foreach (var callback in callbacks) callback();
    }

    private const string Motion = "[{\"res_name\":\"3.1.85\",\"value\":1}]";

    [Fact]
    public void Miio_Device_Can_Be_Disposed_Through_Using()
    {
        var transport = new Mock<IMiioTransport>();
        using (MiioDevice device = new Mock<MiioDevice>(transport.Object, 0) { CallBase = true }.Object)
        {
            device.Should().BeAssignableTo<IDisposable>();
        }
        transport.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Cancelled_Event_Wait_Still_Observes_Late_Subscriber_Failure()
    {
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> subscriber = () => release.Task;
        var notification = AsyncEvents.InvokeAsync(subscriber, handler => handler(),
            exception => error.SetResult(exception), cancellation.Token);

        cancellation.Cancel();
        Func<Task> wait = async () => await notification.WaitAsync(TimeSpan.FromSeconds(1));
        await wait.Should().ThrowAsync<OperationCanceledException>();
        var failure = new InvalidOperationException("late failure");
        release.SetException(failure);
        (await error.Task.WaitAsync(TimeSpan.FromSeconds(1))).Should().BeSameAs(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_Waits_For_Active_Timer_Operation(bool motion)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var timer = new Mock<ITimer>();
        Action elapsed = null;
        timer.Setup(value => value.Start(It.IsAny<Action>())).Callback<Action>(callback => elapsed = callback);
        using GatewaySubDeviceBase sensor = motion
            ? new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance, _ => timer.Object)
            : new XiaomiWindowDoorSensor("contact", 0, NullLoggerFactory.Instance, timer.Object, new Mock<ITimer>().Object);
        ((ProtocolMappedDeviceBase)sensor).InitializeProtocol(DeviceProtocol.LumiMqtt);
        sensor.ParseData(Motion);
        var calls = 0;
        timer.Setup(value => value.Stop()).Callback(() =>
        {
            if (Interlocked.Increment(ref calls) != 1) return;
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        });
        var callback = Task.Run(elapsed);
        entered.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        var disposal = Task.Run(sensor.Dispose);
        try { (await Task.WhenAny(disposal, Task.Delay(50))).Should().NotBe(disposal); }
        finally { release.Set(); }
        await Task.WhenAll(callback, disposal).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Motion_Handler_Disposing_Sensor_Stops_Already_Started_Timers()
    {
        var timer = new Mock<ITimer>();
        using var sensor = new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance, _ => timer.Object);
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        sensor.OnMotionAsync += () => { sensor.Dispose(); return Task.CompletedTask; };

        sensor.ParseData(Motion);

        timer.Verify(value => value.Start(It.IsAny<Action>()), Times.Exactly(MotionSensorBase.NoMotionPeriods.Count));
        timer.Verify(value => value.Dispose(), Times.Exactly(MotionSensorBase.NoMotionPeriods.Count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_Motion_Handler_Cannot_Restart_Obsolete_Timers(bool dispose)
    {
        var timer = new Mock<ITimer>();
        using var sensor = new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance, _ => timer.Object);
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        var release = new TaskCompletionSource<bool>();
        var calls = 0;
        sensor.OnMotionAsync += () => ++calls == 1 ? release.Task : Task.CompletedTask;
        sensor.ParseData(Motion);
        if (dispose) sensor.Dispose();
        else sensor.ParseData(Motion);
        timer.Invocations.Clear();
        release.SetResult(true);
        timer.Verify(value => value.Start(It.IsAny<Action>()), Times.Never);
    }

    [Fact]
    public async Task Each_Event_Subscriber_Runs_And_All_Failures_Are_Observed()
    {
        using var logs = new ErrorRecordingLoggerFactory();
        var asynchronousFailure = new InvalidOperationException("async-first");
        var synchronousFailure = new InvalidOperationException("sync-second");
        var asyncLogged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var syncLogged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        logs.ErrorRecorded += error =>
        {
            if (ReferenceEquals(error, asynchronousFailure)) asyncLogged.TrySetResult(true);
            if (ReferenceEquals(error, synchronousFailure)) syncLogged.TrySetResult(true);
        };
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL,
            "test", 0, logs, out var device).Should().BeTrue();
        using var sensor = (XiaomiTemperatureHumiditySensor)device;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sensor.OnTemperatureChangeAsync += async _ => { await release.Task; throw asynchronousFailure; };
        sensor.OnTemperatureChangeAsync += _ => throw synchronousFailure;
        sensor.OnTemperatureChangeAsync += _ => { completed.SetResult(true); return Task.CompletedTask; };
        sensor.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2100}]");
        try { await completed.Task.WaitAsync(TimeSpan.FromSeconds(1)); }
        finally { release.TrySetResult(true); }
        await Task.WhenAll(asyncLogged.Task, syncLogged.Task).WaitAsync(TimeSpan.FromSeconds(1));
        var failures = logs.TakeErrors();
        failures.Should().HaveCount(2)
            .And.Contain(error => ReferenceEquals(error, asynchronousFailure))
            .And.Contain(error => ReferenceEquals(error, synchronousFailure));
    }

    [Fact]
    public void Disposed_Contact_Sensor_Disposes_Timers_And_Ignores_Late_Elapsed()
    {
        var first = new Mock<ITimer>();
        var second = new Mock<ITimer>();
        Action elapsed = null;
        first.Setup(timer => timer.Start(It.IsAny<Action>())).Callback<Action>(callback => elapsed = callback);
        var sensor = new XiaomiWindowDoorSensor("contact", 0, NullLoggerFactory.Instance, first.Object, second.Object);
        var events = 0;
        sensor.NotClosedFor1MinuteAsync += () => { events++; return Task.CompletedTask; };
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        sensor.ParseData(Motion);
        sensor.Dispose();
        sensor.Dispose();
        elapsed();
        first.Verify(timer => timer.Dispose(), Times.Once);
        second.Verify(timer => timer.Dispose(), Times.Once);
        events.Should().Be(0);
    }

    [Fact]
    public void Disposed_Motion_Sensor_Disposes_All_Period_Timers()
    {
        var timers = new List<Mock<ITimer>>();
        var callbacks = new List<Action>();
        var sensor = new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance, _ =>
        {
            var timer = new Mock<ITimer>();
            timer.Setup(value => value.Start(It.IsAny<Action>())).Callback<Action>(callbacks.Add);
            timers.Add(timer);
            return timer.Object;
        });
        var events = 0;
        sensor.OnNoMotionAsync += _ => { events++; return Task.CompletedTask; };
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        sensor.ParseData(Motion);
        sensor.Dispose();
        foreach (var timer in timers)
        {
            timer.Verify(value => value.Dispose(), Times.Once);
        }
        foreach (var callback in callbacks) callback();
        events.Should().Be(0);
    }
}
