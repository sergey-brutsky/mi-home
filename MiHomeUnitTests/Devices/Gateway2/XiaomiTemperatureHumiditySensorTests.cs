using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Xunit;
using UnifiedTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;

namespace MiHomeUnitTests.Devices.Gateway2;

public class XiaomiTemperatureHumiditySensorTests: Gateway2DeviceTestBase
{
    private readonly string _sid = "158d000156a94b";
    private readonly int _shortId = 12345;

    private readonly UnifiedTemperatureHumiditySensor _thSensor;

    public XiaomiTemperatureHumiditySensorTests()
    {
        ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2,
            UnifiedTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL,
            _sid,
            _shortId,
            _loggerFactory,
            out var device).Should().BeTrue();
        _thSensor = (UnifiedTemperatureHumiditySensor)device;
    }

    [Fact]
    public void Check_ThSensor_Raised_Temperature_And_Humdity_Change()
    {
        // Arrange: the sensor already knows its previous values
        HandleHeartbeat(2995, "1550", "7000");

        float? oldTemperature = null;
        float? oldHumidity = null;

        _thSensor.OnTemperatureChangeAsync += (t) =>
        {
            oldTemperature = t;
            _thSensor.Temperature.Should().Be(16.01f, "the property already holds the new value");
            return Task.CompletedTask;
        };

        _thSensor.OnHumidityChangeAsync += (h) =>
        {
            oldHumidity = h;
            _thSensor.Humidity.Should().Be(72.14f, "the property already holds the new value");
            return Task.CompletedTask;
        };

        // Act
        HandleHeartbeat(2995, "1601", "7214");

        // Assert: change events pass the previous value
        oldTemperature.Should().Be(15.5f);
        oldHumidity.Should().Be(70f);
    }

    private void HandleHeartbeat(int voltage, string temperature, string humidity)
    {
        var cmd = CreateCommand("heartbeat", UnifiedTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "voltage", voltage },
                    { "temperature", temperature },
                    { "humidity", humidity },
                });

        _thSensor.ParseData(Gateway2Message.Parse(cmd).Data);
    }

    [Fact]
    public void Check_ThSensor_Humidity_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", UnifiedTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "humidity", "7159" }
                });

        // Act
        _thSensor.ParseData(Gateway2Message.Parse(cmd).Data);

        // Assert
        Assert.Equal(_sid, _thSensor.DeviceId);
        Assert.Equal(71.59f, _thSensor.Humidity);
    }

    [Fact]
    public void Check_ThSensor_Temperature_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", UnifiedTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "temperature", "1602" }
                });

        // Act
        _thSensor.ParseData(Gateway2Message.Parse(cmd).Data);

        // Assert
        Assert.Equal(_sid, _thSensor.DeviceId);
        Assert.Equal(16.02f, _thSensor.Temperature);
    }

    [Fact]
    public void Check_ThSensor_Hearbeat_Data()
    {
        // Arrange
        var cmd = CreateCommand("heartbeat", UnifiedTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "voltage", 2995 },
                    { "temperature", "1601" },
                    { "humidity", "7214" },
                });

        // Act
        _thSensor.ParseData(Gateway2Message.Parse(cmd).Data);

        // Assert
        Assert.Equal(_sid, _thSensor.DeviceId);
        Assert.Equal(2.995f, _thSensor.Voltage);
        Assert.Equal(16.01f, _thSensor.Temperature);
        Assert.Equal(72.14f, _thSensor.Humidity);
        _thSensor.ToString().Should().Contain("Voltage:")
            .And.NotContain("BatteryPercent:")
            .And.NotContain("LinkQuality:")
            .And.NotContain("ChipTemperature:");
    }
}
