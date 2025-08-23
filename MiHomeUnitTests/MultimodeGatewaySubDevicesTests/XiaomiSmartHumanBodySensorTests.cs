using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Contracts;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class XiaomiSmartHumanBodySensorTests: MultimodeGatewayDeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnMotionDetected_Event(string data)
    {
        // Arrange  
        var motionSensor = _fixture.Create<XiaomiSmartHumanBodySensor>();        
        var eventRaised = false;

        motionSensor.OnMotionDetectedAsync += () => 
        { 
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        motionSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    public void Check_OnNoMotionDetected_For_One_Minute_Event(string data)
    {
        // Arrange  
        var did = _fixture.Create<string>();
        var timer1 = new Mock<ITimer>();
        var timer2 = new Mock<ITimer>();

        timer1
            .Setup(timer => timer.Start())
            .Raises(x => x.Elapsed += null, null, null);

        var motionSensor = new XiaomiSmartHumanBodySensor(did, new NullLoggerFactory(), timer1.Object, timer2.Object);
        
        var eventRaised = false;

        motionSensor.OnNoMotionDetectedAsync += (noMotionInterval) => 
        { 
            eventRaised = true;
            noMotionInterval.Should().Be(XiaomiSmartHumanBodySensor.NoMotionInterval.NoMotionForOneMinute);
            return Task.CompletedTask;
        };

        // Act
        motionSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    public void Check_OnNoMotionDetected_For_Two_Minutes_Event(string data)
    {
        // Arrange  
        var did = _fixture.Create<string>();
        var timer1 = new Mock<ITimer>();
        var timer2 = new Mock<ITimer>();

        timer2
            .Setup(timer => timer.Start())
            .Raises(x => x.Elapsed += null, null, null);

        var motionSensor = new XiaomiSmartHumanBodySensor(did, new NullLoggerFactory(), timer1.Object, timer2.Object);
        
        var eventRaised = false;

        motionSensor.OnNoMotionDetectedAsync += (noMotionInterval) => 
        { 
            eventRaised = true;
            noMotionInterval.Should().Be(XiaomiSmartHumanBodySensor.NoMotionInterval.NoMotionForTwoMinutes);
            return Task.CompletedTask;
        };

        // Act
        motionSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
