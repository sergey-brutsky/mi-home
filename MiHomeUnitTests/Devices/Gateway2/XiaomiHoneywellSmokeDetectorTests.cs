using MiHomeLib.Protocols;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class XiaomiHoneywellSmokeDetectorTests: Gateway2DeviceTestBase
{
    private readonly string _sid = "158d0001d8f8f7";
    private readonly int _shortId = 12345;
    private readonly XiaomiHoneywellSmokeDetector _device;

    public XiaomiHoneywellSmokeDetectorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, XiaomiHoneywellSmokeDetector.XIAOMI_GATEWAY2_MODEL, _sid, _shortId, _loggerFactory, out var device));
        _device = Assert.IsType<XiaomiHoneywellSmokeDetector>(device);
    }

    private void Parse(string cmd, Dictionary<string, object> data) =>
        _device.ParseData(Gateway2Message.Parse(
            CreateCommand(cmd, XiaomiHoneywellSmokeDetector.XIAOMI_GATEWAY2_MODEL, _sid, _shortId, data)).Data);

    [Fact]
    public void Check_SmokeSensor_NoAlarm_Event_Raised()
    {
        Parse("report", new() { { "alarm", "1" } });
        var alarmStoppedEventRaised = false;
        _device.OnAlarmStoppedAsync += () => { alarmStoppedEventRaised = true; return Task.CompletedTask; };

        Parse("report", new() { { "alarm", "0" } });

        _device.Alarm.Should().BeFalse();
        alarmStoppedEventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_SmokeSensor_Alarm_Event_Raised()
    {
        Parse("report", new() { { "alarm", "0" } });
        var alarmEventRaised = false;
        _device.OnAlarmAsync += () => { alarmEventRaised = true; return Task.CompletedTask; };

        Parse("report", new() { { "alarm", "1" } });

        _device.Alarm.Should().BeTrue();
        alarmEventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_SmokeSensor_ReadAck_Data()
    {
        Parse("read_ack", new() { { "voltage", 3055 }, { "alarm", "0" } });

        _device.DeviceId.Should().Be(_sid);
        _device.ShortId.Should().Be(_shortId);
        _device.Voltage.Should().Be(3.055f);
        _device.Alarm.Should().BeFalse();
        _device.ToString().Should().Be($"Device: JTYJ-GD-01LM/BW smoke {_sid}, Voltage: {3.055f}V, Alarm: False, Smoke density: 0");
    }

    [Fact]
    public void Density_Is_Taken_As_Reported_And_Passes_Old_Value()
    {
        Parse("report", new() { { "density", "3" } });
        int? oldValue = null;
        _device.OnSmokeDensityChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        Parse("report", new() { { "density", "12" } });

        _device.SmokeDensity.Should().Be(12);
        oldValue.Should().Be(3);
    }

    [Fact]
    public void Commands_Are_Not_Supported_On_Gateway2()
    {
        _device.Invoking(x => x.SetSensivity(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke))
            .Should().Throw<NotSupportedException>();
    }
}
