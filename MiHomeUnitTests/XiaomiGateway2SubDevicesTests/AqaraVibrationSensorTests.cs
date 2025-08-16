using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class AqaraVibrationSensorTests : Gw2DeviceTests
{
    private readonly AqaraVirationSensor _device; 
    public AqaraVibrationSensorTests()
    {
        _device = new AqaraVirationSensor(_fixture.Create<string>(), _fixture.Create<byte>(), _loggerFactory);
    }
    
    [Fact]
    public void Check_Vibration_Raised()
    {
        // Arrange
        bool eventRaised = false;

        _device.OnVibrationAsync += () => 
        { 
            eventRaised = true; 
            return Task.CompletedTask;
        };
        
        // Act
        _device.ParseData($"{{\"status\":\"vibrate\"}}");

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_FreeFall_Raised()
    {
        // Arrange
        bool eventRaised = false;
        
        _device.OnFreeFallAsync += () => 
        {
            eventRaised = true;
            return Task.CompletedTask;
        };
        
        // Act
        _device.ParseData($"{{\"status\":\"free_fall\"}}");

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_Tilt_Raised()
    {
        // Arrange
        bool eventRaised = false;

        _device.OnTiltAsync += () => 
        {
            eventRaised = true;
            return Task.CompletedTask;
        };
        
        // Act
        _device.ParseData($"{{\"status\":\"tilt\"}}");

        // Assert
        eventRaised.Should().BeTrue();
    }
    
    [Fact]
    public void Check_FinalTiltAngle_Raised()
    {
        // Arrange
        bool eventRaised = false;
        var expectedAngle = 170;
        var actualAngle = 0;

        _device.OnFinalTiltAngleAsync += (finalTitleAngle) => 
        {
            eventRaised = true;
            actualAngle = finalTitleAngle;
            return Task.CompletedTask;
        };
        
        // Act                        
        _device.ParseData($"{{\"final_tilt_angle\":\"{expectedAngle}\"}}");

        // Assert
        eventRaised.Should().BeTrue();
        actualAngle.Should().Be(expectedAngle);
    }

    [Fact]
    public void Check_Coordination_Raised()
    {
        // Arrange
        bool eventRaised = false;
        var (x, y, z) = (1, 2, 3);
        var (x1, y1, z1) = (0, 0, 0);

        _device.OnCoordinationsAsync += (coords) => 
        {
            eventRaised = true;
            (x1, y1, z1) = coords;
            return Task.CompletedTask;
        };
        
        // Act                        
        _device.ParseData($"{{\"coordination\":\"{x},{y},{z}\"}}");

        // Assert
        eventRaised.Should().BeTrue();
        (x,y,z).Should().Be((x1, y1, z1));
    }

    [Fact]
    public void Check_BedActivity_Raised()
    {
        // Arrange
        bool eventRaised = false;
        var expectedBedActivity = 200;
        var actualBedActivity = 0;

        _device.OnBedActivityAsync += (bedActivity) => 
        {
            eventRaised = true;
            actualBedActivity = bedActivity;
            return Task.CompletedTask;
        };
        
        // Act                        
        _device.ParseData($"{{\"bed_activity\":\"{expectedBedActivity}\"}}");

        // Assert
        eventRaised.Should().BeTrue();
        actualBedActivity.Should().Be(expectedBedActivity);
    }

    [Fact]
    public void Check_Voltage()
    {
        // Arrange
        float expected = 3.005f;

        // Act                        
        _device.ParseData($"{{\"voltage\":3005}}");

        // Assert
        _device.Voltage.Should().Be(expected);
    }
}