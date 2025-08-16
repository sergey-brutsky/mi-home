using Xunit;
using AutoFixture;
using FluentAssertions;
using MiHomeLib.XiaomiGateway3.Devices;
using System.Threading.Tasks;

namespace MiHomeUnitTests.XiaomiGateway3SubDevices;
public class XiaomiBluetoothHygrothermograph2Tests: Gw3DeviceTests
{
    private readonly XiaomiBluetoothHygrothermograph2 _th;

    public XiaomiBluetoothHygrothermograph2Tests() => _th = _fixture.Build<XiaomiBluetoothHygrothermograph2>().Create();

    [Theory]
    [InlineData(4100, "E500", 24.1f)]
    [InlineData(4100, "fb00", 22f)]
    public void Check_OnTemperatureChange_Event(int eid, string edata, float oldTemperature)
    {
        // Arrange       
        var eventRaised = false;
        
        _th.Temperature = oldTemperature;
        
        _th.OnTemperatureChangeAsync += (oldValue) =>
         { 
            eventRaised = oldValue == oldTemperature;
            _th.Temperature.Should().Be(_th.ToBleFloat(edata));
            return Task.CompletedTask;
        };
        
        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4102, "E601", 44.1f)]
    [InlineData(4102, "e301", 33.2f)]
    public void Check_OnHumidityChange_Event(int eid, string edata, float oldHumidity)
    {
        // Arrange       
        var eventRaised = false;
        
        _th.Humidity = oldHumidity;
        
        _th.OnHumidityChangeAsync += (oldValue) =>
         { 
            eventRaised = oldValue == oldHumidity;
            _th.Humidity.Should().Be(_th.ToBleFloat(edata));
            return Task.CompletedTask;
        };
        
        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
