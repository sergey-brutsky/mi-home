using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiTemperatureHumiditySensorTests: Gw2DeviceTests
{
    private readonly string _sid = "158d000156a94b";
    private readonly int _shortId = 12345;

    private readonly XiaomiTemperatureHumiditySensor _thSensor;

    public XiaomiTemperatureHumiditySensorTests()
    {
        _thSensor = new XiaomiTemperatureHumiditySensor(_sid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_ThSensor_Raised_Temperature_And_Humdity_Change()
    {
        // Arrange
        bool temperatureEventRaised = false;
        bool humidityEventRaised = false;
        float newTemperature = 0;
        float newHumidity = 0;

        _thSensor.OnTemperatureChangeAsync += (t) =>
        {
            temperatureEventRaised = true;
            newTemperature = t;
            return Task.CompletedTask;
        };

        _thSensor.OnHumidityChangeAsync += (h) =>
        {
            humidityEventRaised = true;
            newHumidity = h;
            return Task.CompletedTask;
        };

        // Act
        var cmd = CreateCommand("heartbeat", XiaomiTemperatureHumiditySensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "voltage", 2995 },
                    { "temperature", "1601" },
                    { "humidity", "7214" },
                });

        _thSensor.ParseData(ResponseCommand.FromString(cmd).Data);
        
        // Assert
        Assert.True(temperatureEventRaised);
        Assert.True(humidityEventRaised);
        Assert.Equal(16.01f, newTemperature);
        Assert.Equal(72.14f, newHumidity);
    }

    [Fact]
    public void Check_ThSensor_Humidity_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", XiaomiTemperatureHumiditySensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "humidity", "7159" }
                });

        // Act
        _thSensor.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _thSensor.Sid);
        Assert.Equal(71.59f, _thSensor.Humidity);
    }

    [Fact]
    public void Check_ThSensor_Temperature_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", XiaomiTemperatureHumiditySensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "temperature", "1602" }
                });

        // Act
        _thSensor.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _thSensor.Sid);
        Assert.Equal(16.02f, _thSensor.Temperature);
    }
    
    [Fact]
    public void Check_ThSensor_Hearbeat_Data()
    {
        // Arrange
        var cmd = CreateCommand("heartbeat", XiaomiTemperatureHumiditySensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "voltage", 2995 },
                    { "temperature", "1601" },
                    { "humidity", "7214" },
                });

        // Act        
        _thSensor.ParseData(ResponseCommand.FromString(cmd).Data);
        
        // Assert
        Assert.Equal(_sid, _thSensor.Sid);
        Assert.Equal(2.995f, _thSensor.Voltage);
        Assert.Equal(16.01f, _thSensor.Temperature);
        Assert.Equal(72.14f, _thSensor.Humidity);
    }
}