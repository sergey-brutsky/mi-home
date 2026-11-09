using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
public class XiaomiBluetoothHygrothermograph2Tests: MqttGatewayDeviceTests
{
    private readonly XiaomiBluetoothHygrothermograph2 _th;

    public XiaomiBluetoothHygrothermograph2Tests() => _th = _fixture.Build<XiaomiBluetoothHygrothermograph2>().Create();

    // Expected values are literals: d400 -> 21.2 is what the gateway itself logged for this sensor
    [Theory]
    [InlineData(4100, "E500", 22.9f, 24.1f)]
    [InlineData(4100, "fb00", 25.1f, 22f)]
    [InlineData(4100, "d400", 21.2f, 22f)]
    [InlineData(4100, "cbff", -5.3f, 1f)]
    public void Check_OnTemperatureChange_Event(int eid, string edata, float expectedTemperature, float oldTemperature)
    {
        // Arrange       
        var eventRaised = false;
        
        _th.Temperature = oldTemperature;
        
        _th.OnTemperatureChangeAsync += (oldValue) =>
         { 
            eventRaised = oldValue == oldTemperature;
            _th.Temperature.Should().Be(expectedTemperature);
            return Task.CompletedTask;
        };
        
        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    // 5102/5202/5302 -> 59.3/59.4/59.5 are values the gateway itself logged for this sensor
    [Theory]
    [InlineData(4102, "E601", 48.6f, 44.1f)]
    [InlineData(4102, "e301", 48.3f, 33.2f)]
    [InlineData(4102, "5102", 59.3f, 33.2f)]
    [InlineData(4102, "5202", 59.4f, 33.2f)]
    [InlineData(4102, "5302", 59.5f, 33.2f)]
    public void Check_OnHumidityChange_Event(int eid, string edata, float expectedHumidity, float oldHumidity)
    {
        // Arrange       
        var eventRaised = false;
        
        _th.Humidity = oldHumidity;
        
        _th.OnHumidityChangeAsync += (oldValue) =>
         { 
            eventRaised = oldValue == oldHumidity;
            _th.Humidity.Should().Be(expectedHumidity);
            return Task.CompletedTask;
        };
        
        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
