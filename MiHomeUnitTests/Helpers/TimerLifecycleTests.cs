using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using Moq;
using Xunit;
using ITimer = MiHomeLib.Devices.Timing.ITimer;

namespace MiHomeUnitTests.Helpers;

public class TimerLifecycleTests
{
    [Fact]
    public void Motion_Countdown_Starts_Before_Client_Handler_Completes()
    {
        var callbacks = new List<Action>();
        var timer = CaptureCallbacks(callbacks);
        var created = 0;
        using var sensor = new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance,
            _ => created++ == 0 ? timer.Object : Mock.Of<ITimer>());
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        var handler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sensor.OnMotionAsync += () => handler.Task;
        var events = 0;
        sensor.OnNoMotionAsync += seconds => { seconds.Should().Be(60); events++; return Task.CompletedTask; };
        sensor.ParseData(Open);
        callbacks.Should().ContainSingle();
        callbacks[0]();
        events.Should().Be(1);
        handler.SetResult(true);
    }

    private const string Open = "[{\"res_name\":\"3.1.85\",\"value\":1}]";
    private const string Close = "[{\"res_name\":\"3.1.85\",\"value\":0}]";

    private static Mock<ITimer> CaptureCallbacks(List<Action> callbacks)
    {
        var timer = new Mock<ITimer>();
        timer.Setup(value => value.Start(It.IsAny<Action>())).Callback<Action>(callbacks.Add);
        return timer;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Previous_Contact_Countdown_Cannot_Fire_After_Close_Or_Reopen(bool reopen, bool fiveMinutes)
    {
        var callbacks = new List<Action>();
        var timer = CaptureCallbacks(callbacks);
        using var sensor = new XiaomiWindowDoorSensor("contact", 0, NullLoggerFactory.Instance,
            fiveMinutes ? Mock.Of<ITimer>() : timer.Object, fiveMinutes ? timer.Object : Mock.Of<ITimer>());
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        var events = 0;
        if (fiveMinutes) sensor.NotClosedFor5MinutesAsync += () => { events++; return Task.CompletedTask; };
        else sensor.NotClosedFor1MinuteAsync += () => { events++; return Task.CompletedTask; };
        sensor.ParseData(Open);
        sensor.ParseData(Close);
        if (reopen) sensor.ParseData(Open);
        timer.Invocations.Clear();

        callbacks[0]();

        events.Should().Be(0);
        timer.Verify(value => value.Stop(), Times.Never);
        if (reopen)
        {
            callbacks[1]();
            callbacks[1]();
            events.Should().Be(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Previous_Motion_Countdown_Cannot_Stop_The_New_Countdown(bool pendingHandler)
    {
        var callbacks = new List<Action>();
        var timer = CaptureCallbacks(callbacks);
        var created = 0;
        using var sensor = new XiaomiSmartHumanBodySensor("motion", 0, NullLoggerFactory.Instance,
            _ => created++ == 0 ? timer.Object : Mock.Of<ITimer>());
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        var events = 0;
        sensor.OnNoMotionAsync += _ => { events++; return Task.CompletedTask; };
        sensor.ParseData(Open);
        if (pendingHandler) sensor.OnMotionAsync += () => new TaskCompletionSource<bool>().Task;
        sensor.ParseData(Open);
        timer.Invocations.Clear();

        callbacks[0]();

        events.Should().Be(0);
        timer.Verify(value => value.Stop(), Times.Never);
        callbacks[1]();
        callbacks[1]();
        events.Should().Be(1);
    }
}
