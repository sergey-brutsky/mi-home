using System.Threading.Tasks;
using AutoFixture;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiWindowDoorSensorTests: Gw2DeviceTests
{
    private readonly XiaomiWindowDoorSensor _device;

    public XiaomiWindowDoorSensorTests()
    {
        _device = new XiaomiWindowDoorSensor(_fixture.Create<string>(), _fixture.Create<byte>(), _loggerFactory);
    }

    [Fact]
    public void Check_DoorWindowSensor_Raised_Open_Event()
    {
        // Arrange
        var eventRaised = false;
        
        _device.OnOpenAsync += () => 
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        _device.ParseData($"{{\"status\":\"open\"}}");

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void Check_DoorWindowSensor_Raised_Closed_Event()
    {
        // Arrange
        var eventRaised = false;
        
        _device.OnCloseAsync += () => 
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        _device.ParseData($"{{\"status\":\"close\"}}");

        Assert.True(eventRaised);
    }

    [Fact]
    public void Check_DoorWindowSensor_Raised_NotClosedFor1Minute_Event()
    {
        // Arrange
        var eventRaised = false;
        
        _device.NotClosedFor1MinuteAsync += () => 
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        _device.ParseData($"{{\"no_close\":\"60\"}}");

        Assert.True(eventRaised);
    }

    [Fact]
    public void Check_DoorWindowSensor_Raised_NotClosedFor5Minutes_Event()
    {
        // Arrange
        var eventRaised = false;
        
        _device.NotClosedFor5MinutesAsync += () => 
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        _device.ParseData($"{{\"no_close\":\"300\"}}");

        Assert.True(eventRaised);
    }
}