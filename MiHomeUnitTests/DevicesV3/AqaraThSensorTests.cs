using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;

public class AqaraThSensorTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"0.1.85\",\"value\":2515}]")]
    public void Check_OnTemperatureChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var th = _fixture.Create<AqaraThSensor>();
        th.Temperature = oldValue;
        
        var eventRaised = false;

        th.OnTemperatureChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.Temperature.Should().Be(DataToZigbeeResource(data)[0].Value/100f);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.2.85\",\"value\":4343}]")]
    public void Check_OnHumidityChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var th = _fixture.Create<AqaraThSensor>();
        th.Humidity = oldValue;
        
        var eventRaised = false;

        th.OnHumidityChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.Humidity.Should().Be(DataToZigbeeResource(data)[0].Value/100f);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.3.85\",\"value\":99120}]")]
    public void Check_OnPressureChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var th = _fixture.Create<AqaraThSensor>();
        th.Pressure = oldValue;
        
        var eventRaised = false;

        th.OnPressureChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.Pressure.Should().Be(DataToZigbeeResource(data)[0].Value/100f);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }   
}
