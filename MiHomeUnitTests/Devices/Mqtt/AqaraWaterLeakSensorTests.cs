using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraWaterLeakSensorTests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]", true)]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]", false)]
    public void Parses_Moisture_And_Raises_Event(string data, bool expectedMoisture)
    {
        var sensor = CreateLumiProtocolDevice<AqaraWaterLeakSensor>(
            AqaraWaterLeakSensor.LUMI_MQTT_MODEL);
        bool? eventValue = null;
        sensor.OnMoistureChangeAsync += value =>
        {
            eventValue = value;
            sensor.Moisture.Should().Be(expectedMoisture);
            return Task.CompletedTask;
        };

        sensor.ParseData(data);

        sensor.Moisture.Should().Be(expectedMoisture);
        eventValue.Should().Be(expectedMoisture);
    }

    [Fact]
    public void Parses_Lumi_Battery_Properties_And_Requests_Them_In_Order()
    {
        var sensor = CreateLumiProtocolDevice<AqaraWaterLeakSensor>(
            AqaraWaterLeakSensor.LUMI_MQTT_MODEL);
        sensor.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");
        sensor.SetInitialProperties(
        [
            System.Text.Json.Nodes.JsonValue.Create(3005),
            System.Text.Json.Nodes.JsonValue.Create(95),
            System.Text.Json.Nodes.JsonValue.Create(132),
            System.Text.Json.Nodes.JsonValue.Create(36)
        ]);
        sensor.ParseData(
            "[{\"res_name\":\"8.0.2008\",\"value\":3010},{\"res_name\":\"8.0.2001\",\"value\":94}," +
            "{\"res_name\":\"8.0.2007\",\"value\":130},{\"res_name\":\"8.0.2006\",\"value\":37}]");

        sensor.Voltage.Should().Be(3.01f);
        sensor.BatteryPercent.Should().Be(94);
        sensor.LinkQuality.Should().Be(130);
        sensor.ChipTemperature.Should().Be(37);
        sensor.ToString().Should().Contain("BatteryPercent:");
    }
}
