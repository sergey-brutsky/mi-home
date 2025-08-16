using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiSmartHumanBodySensorTests : Gw2DeviceTests
{
    private readonly string _sid = "158d0001214e1f";
    private readonly int _shortId = 12345;
    private readonly XiaomiSmartHumanBodySensor _device;

    public XiaomiSmartHumanBodySensorTests()
    {
        _device = new XiaomiSmartHumanBodySensor(_sid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_MotionSensor_ReadAck_Data()
    {
        // Arrange
        var cmd = CreateCommand("read_ack", "motion", _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "voltage", 2985 }
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _device.Sid);
        Assert.Equal(2.985f, _device.Voltage);
    }

    [Fact]
    public void Check_MotionSensor_Reports_NoMotion_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", "motion", _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "no_motion", "120" }
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        var noMotionRaised = false;
        var noMotionSeconds = 0;

        _device.OnNoMotionAsync += (noMotionPeriod) =>
        {
            noMotionRaised = true;
            noMotionSeconds = noMotionPeriod;
            return Task.CompletedTask;
        };

        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _device.Sid);
        Assert.True(noMotionRaised);
        Assert.Equal(120, noMotionSeconds);
    }

    [Fact]
    public void Check_MotionSensor_Reports_Motion_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", "motion", _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "status", "motion" }
                });

        // Act
        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        var motionRaised = false;

        _device.OnMotionAsync += () =>
        {
            motionRaised = true;
            return Task.CompletedTask;
        };

        _device.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _device.Sid);
        Assert.True(motionRaised);
    }
}