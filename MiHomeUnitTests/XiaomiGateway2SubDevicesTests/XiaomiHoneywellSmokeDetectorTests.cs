using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiHoneywellSmokeDetectorTests: Gw2DeviceTests
{
    private readonly string _sid = "158d0001d8f8f7";
    private readonly int _shortId = 12345;
    private readonly XiaomiHoneywellSmokeDetector _device;

    public XiaomiHoneywellSmokeDetectorTests()
    {
        _device = new XiaomiHoneywellSmokeDetector(_sid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_SmokeSensor_NoAlarm_Event_Raised()
    {
        // Arrange
        var cmd = CreateCommand("report", "smoke", "", 25885,
                new Dictionary<string, object>
                {
                    { "alarm", "1" },
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        var alarmStoppedEventRaised = false;

        _device.OnAlarmStoppedAsync += () =>
        {
            alarmStoppedEventRaised = true;
            return Task.CompletedTask;
        };

        var cmd1 = CreateCommand("report", "smoke", _sid, 25885,
                new Dictionary<string, object>
                {
                    { "alarm", "0" },
                });

        _device.ParseData(ResponseCommand.FromString(cmd1).Data);

        // Assert
        Assert.False(_device.Alarm);
        Assert.True(alarmStoppedEventRaised);
    }

    [Fact]
    public void Check_SmokeSensor_Alarm_Event_Raised()
    {
        // Arrange
        var cmd = CreateCommand("report", "smoke", _sid, 25885,
                new Dictionary<string, object>
                {
                    { "alarm", "0" },
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        var alarmEventRaised = false;

        _device.OnAlarmAsync += () =>
        {
            alarmEventRaised = true;
            return Task.CompletedTask;
        };

        var cmd1 = CreateCommand("report", "smoke", _sid, 25885,
                new Dictionary<string, object>
                {
                    { "alarm", "1" },
                });

        _device.ParseData(ResponseCommand.FromString(cmd1).Data);

        // Assert
        Assert.True(_device.Alarm);
        Assert.True(alarmEventRaised);
    }

    [Fact]
    public void Check_SmokeSensor_Heartbeat_Data()
    {
        // Arrange
        var cmd = CreateCommand("heartbeat", "smoke", _sid, 25885,
                new Dictionary<string, object>
                {
                    { "voltage", 3065 },
                    { "alarm", "0" },
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _device.Sid);
        Assert.Equal(3.065f, _device.Voltage);
        Assert.False(_device.Alarm);
    }

    [Fact]
    public void Check_SmokeSensor_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", "smoke", _sid, 25885,
                new Dictionary<string, object>
                {
                    { "density", "12" }
                });
        
        var eventRaised = false;
        float density = 0;

        _device.OnDensityChangeAsync += (x) =>
        {
            eventRaised = true;
            density = x;
            return Task.CompletedTask;
        };

        // Act        
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _device.Sid);
        Assert.True(eventRaised);
        Assert.Equal(0.12f, density);
    }

    [Fact]
    public void Check_SmokeSensor_ReadAck_Data()
    {
        // Arrange
        var cmd = CreateCommand("read_ack", "smoke", _sid, 25885,
                new Dictionary<string, object>
                {
                    { "voltage", 3055 },
                    { "alarm", "0" }
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _device.Sid);
        Assert.Equal(3.055f, _device.Voltage);
        Assert.False(_device.Alarm);
    }
}
