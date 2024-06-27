using MiHomeLib;
using Xunit;
using AutoFixture;
using FluentAssertions;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;
public class MiThMonitor2Tests: MiHome3DeviceTests
{
    private readonly MiThMonitor2 _thMonitor2;

    public MiThMonitor2Tests() => _thMonitor2 = _fixture.Build<MiThMonitor2>().Create();

    [Theory]
    [InlineData(4100, "E500", 24.1f)]
    [InlineData(4100, "fb00", 22f)]
    public void Check_OnTemperatureChange_Event(int eid, string edata, float oldTemperature)
    {
        // Arrange       
        var eventRaised = false;
        
        _thMonitor2.Temperature = oldTemperature;
        
        _thMonitor2.OnTemperatureChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldTemperature;
            _thMonitor2.Temperature.Should().Be(edata.ToBleFloat());
        };
        
        // Act
        _thMonitor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

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
        
        _thMonitor2.Humidity = oldHumidity;
        
        _thMonitor2.OnHumidityChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldHumidity;
            _thMonitor2.Humidity.Should().Be(edata.ToBleFloat());
        };
        
        // Act
        _thMonitor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
