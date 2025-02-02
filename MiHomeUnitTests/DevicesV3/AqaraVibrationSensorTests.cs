using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using System.Collections.Generic;
using Moq;

namespace MiHomeUnitTests.DevicesV3;
public class AqaraVibrationSensorTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"0.2.85\",\"value\":61}]")]
    public void Check_OnFinalTiltChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<int>();
        var vs = new AqaraVirationSensor(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            FinalTiltAngle = oldValue
        };
        
        var eventRaised = false;

        vs.OnFinalTiltAngle += (x) => 
        { 
            eventRaised = x == oldValue;
            vs.FinalTiltAngle.Should().Be(DataToZigbeeResource(data)[0].Value);
        };

        // Act
        vs.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.1.85\",\"value\":212}]")]
    public void Check_OnBedActivityChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<int>();
        var vs = new AqaraVirationSensor(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            BedActivity = oldValue
        };
        
        var eventRaised = false;

        vs.OnBedActivity += (x) => 
        { 
            eventRaised = x == oldValue;
            vs.BedActivity.Should().Be(DataToZigbeeResource(data)[0].Value);
        };

        // Act
        vs.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":2}]")]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":3}]")]
    public void Check_OnStatusChange_Event(string data)
    {
        // Arrange  
        var vs = new AqaraVirationSensor(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory);
        var val = DataToZigbeeResource(data)[0].Value;
        
        var map = new Dictionary<int, bool>()
        {
            {1, false},
            {2, false},
            {3, false},
        };
        
        vs.OnVibration += () => map[1] = true;
        vs.OnTilt += () => map[2] = true;
        vs.OnFreeFall += () => map[3] = true;

        // Act
        vs.ParseData(data);

        // Assert
        map[val].Should().BeTrue();
    }

    [Theory]
    [InlineData(AqaraVirationSensor.SensivityState.Low, 21)]
    [InlineData(AqaraVirationSensor.SensivityState.Middle, 11)]
    [InlineData(AqaraVirationSensor.SensivityState.High, 1)]
    public void Check_SetSensivity_Works(AqaraVirationSensor.SensivityState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}";
        var vs = new AqaraVirationSensor(did, _mqttTransport.Object, _loggerFactory);

        // Act
        vs.SetSensivity(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
