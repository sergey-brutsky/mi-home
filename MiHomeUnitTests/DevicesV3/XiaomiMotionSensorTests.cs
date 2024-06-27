using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace MiHomeUnitTests.DevicesV3;

public class XiaomiMotionSensorTests: MiHome3DeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnMotionDetected_Event(string data)
    {
        // Arrange  
        var motionSensor = _fixture
                    .Build<XiaomiMotionSensor>()
                    .Create();
        
        var eventRaised = false;

        motionSensor.OnMotionDetected += () => 
        { 
            eventRaised = true;
            motionSensor.MotionDetected.Should().Be(DataToZigbeeResource(data)[0].Value == 1);
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
        var timer1 = new Mock<MiHomeLib.Utils.ITimer>();
        var timer2 = new Mock<MiHomeLib.Utils.ITimer>();

        timer1
            .Setup(timer => timer.Start())
            .Raises(x => x.Elapsed += null, null, null);

        var motionSensor = new XiaomiMotionSensor(did, new NullLoggerFactory(), timer1.Object, timer2.Object);
        
        var eventRaised = false;

        motionSensor.OnNoMotionDetected += (noMotionInterval) => 
        { 
            eventRaised = true;
            noMotionInterval.Should().Be(XiaomiMotionSensor.NoMotionInterval.NoMotionForOneMinute);
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
        var timer1 = new Mock<MiHomeLib.Utils.ITimer>();
        var timer2 = new Mock<MiHomeLib.Utils.ITimer>();

        timer2
            .Setup(timer => timer.Start())
            .Raises(x => x.Elapsed += null, null, null);

        var motionSensor = new XiaomiMotionSensor(did, new NullLoggerFactory(), timer1.Object, timer2.Object);
        
        var eventRaised = false;

        motionSensor.OnNoMotionDetected += (noMotionInterval) => 
        { 
            eventRaised = true;
            noMotionInterval.Should().Be(XiaomiMotionSensor.NoMotionInterval.NoMotionForTwoMinutes);
        };

        // Act
        motionSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
