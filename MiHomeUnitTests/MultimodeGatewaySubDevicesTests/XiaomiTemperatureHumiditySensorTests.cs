using AutoFixture;
using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
public class XiaomiTemperatureHumiditySensorTests: MultimodeGatewayDeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"0.1.85\",\"value\":2515}]")]
    public void Check_OnTemperatureChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var th = _fixture.Create<XiaomiTemperatureHumiditySensor>();
        th.Temperature = oldValue;
        
        var eventRaised = false;

        th.OnTemperatureChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            th.Temperature.Should().Be(DataToZigbeeResource(data)[0].Value/100f);
            return Task.CompletedTask;
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
        var th = _fixture.Create<XiaomiTemperatureHumiditySensor>();
        th.Humidity = oldValue;
        
        var eventRaised = false;

        th.OnHumidityChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            th.Humidity.Should().Be(DataToZigbeeResource(data)[0].Value/100f);
            return Task.CompletedTask;
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }    
}
