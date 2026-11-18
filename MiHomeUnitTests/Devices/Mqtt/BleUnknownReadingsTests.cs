using System;
using System.Text.Json;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class BleUnknownReadingsTests : ProtocolDeviceTestBase
{
    private GatewaySubDeviceBase CreateSensor(bool miSpec) => miSpec
        ? CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph3>(XiaomiBluetoothHygrothermograph3.PDID)
        : CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);

    private string Report(bool miSpec, int property, int value) => miSpec
        ? JsonSerializer.Serialize(new { siid = property == 1003 ? 2 : 3, piid = property, value })
        : SetupBleAsyncEventParams(property == 1001 ? 4100 : property == 1002 ? 4102 : 4106,
            property == 1003 ? value.ToString("x2") : "0000").ToString();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Each_Reading_Becomes_Known_Only_After_Its_Report(bool miSpec)
    {
        var sensor = CreateSensor(miSpec);
        sensor.ToString().Should().Contain("Temperature: Unknown")
            .And.Contain("Humidity: Unknown").And.Contain("Battery Percent: Unknown");

        sensor.ParseData(Report(miSpec, 1001, 0));
        sensor.ToString().Should().Contain("Temperature: 0°C")
            .And.Contain("Humidity: Unknown").And.Contain("Battery Percent: Unknown");

        sensor.ParseData(Report(miSpec, 1002, 0));
        sensor.ToString().Should().Contain("Humidity: 0%")
            .And.Contain("Battery Percent: Unknown");

        sensor.ParseData(Report(miSpec, 1003, 0));
        sensor.ToString().Should().Contain("Battery Percent: 0%").And.NotContain("Unknown");

        CreateSensor(miSpec).ToString().Should().Contain("Temperature: Unknown");
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(1002)]
    [InlineData(1003)]
    public void Invalid_MiSpec_Value_Does_Not_Mark_Reading_As_Known(int property)
    {
        var sensor = CreateSensor(true);
        var before = sensor.ToString();
        sensor.ParseData(JsonSerializer.Serialize(new
        {
            siid = property == 1003 ? 2 : 3, piid = property, value = "invalid"
        }));
        sensor.ToString().Should().Be(before);
    }

    [Theory]
    [InlineData(4100)]
    [InlineData(4102)]
    [InlineData(4106)]
    public void Invalid_MiBeacon_Length_Does_Not_Mark_Reading_As_Known(int eid)
    {
        var sensor = CreateSensor(false);
        var before = sensor.ToString();
        Action parse = () => sensor.ParseData(SetupBleAsyncEventParams(eid, "000000").ToString());
        parse.Should().NotThrow();
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains(eid.ToString()));
        sensor.ToString().Should().Be(before);
    }

    [Fact]
    public void Smoke_Sensor_Battery_Description_Distinguishes_Unknown_From_Zero()
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiSmokeSensorGuard>(XiaomiSmokeSensorGuard.PDID);
        sensor.ToString().Should().Contain("BatteryPercent: Unknown");
        sensor.ParseData(SetupBleAsyncEventParams(4106, "00").ToString());
        sensor.ToString().Should().Contain("BatteryPercent: 0%");
        sensor.ParseData(SetupBleAsyncEventParams(4106, "64").ToString());
        sensor.ToString().Should().Contain("BatteryPercent: 100%");
    }
}
