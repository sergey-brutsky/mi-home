using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraVibrationSensorTests : MqttDeviceTestBase
{
    private AqaraVibrationSensor CreateSensor()
    {
        var sensor = CreateLumiProtocolDevice<AqaraVibrationSensor>(AqaraVibrationSensor.LUMI_MQTT_MODEL);
        ((IProtocolMappedManageableDevice)sensor).SetMqttTransport(_mqttTransport.Object);
        return sensor;
    }

    [Theory]
    [InlineData(1, "vibration")]
    [InlineData(2, "tilt")]
    [InlineData(3, "free_fall")]
    public void Status_Raises_Matching_Event(int value, string expected)
    {
        var sensor = CreateSensor();
        var raised = new List<string>();
        sensor.OnVibrationAsync += () => { raised.Add("vibration"); return Task.CompletedTask; };
        sensor.OnTiltAsync += () => { raised.Add("tilt"); return Task.CompletedTask; };
        sensor.OnFreeFallAsync += () => { raised.Add("free_fall"); return Task.CompletedTask; };

        sensor.ParseData($"[{{\"res_name\":\"13.1.85\",\"value\":{value}}}]");

        raised.Should().Equal(expected);
    }

    [Fact]
    public void Tilt_Angle_And_Bed_Activity_Pass_Old_Values()
    {
        var sensor = CreateSensor();
        sensor.ParseData("[{\"res_name\":\"0.2.85\",\"value\":30},{\"res_name\":\"0.1.85\",\"value\":10}]");
        int? oldAngle = null, oldActivity = null;
        sensor.OnFinalTiltAngleAsync += x => { oldAngle = x; return Task.CompletedTask; };
        sensor.OnBedActivityAsync += x => { oldActivity = x; return Task.CompletedTask; };

        sensor.ParseData("[{\"res_name\":\"0.2.85\",\"value\":120},{\"res_name\":\"0.1.85\",\"value\":200}]");

        sensor.FinalTiltAngle.Should().Be(120);
        sensor.BedActivity.Should().Be(200);
        oldAngle.Should().Be(30);
        oldActivity.Should().Be(10);
    }

    [Fact]
    public void Initial_Properties_Are_Battery_Device_Ones()
    {
        var sensor = CreateSensor();
        sensor.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");

        sensor.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(95), JsonValue.Create(120), JsonValue.Create(25)]);

        sensor.Voltage.Should().Be(3.005f);
        sensor.BatteryPercent.Should().Be(95);
    }

    [Theory]
    [InlineData(AqaraVibrationSensor.SensitivityState.Low, 21)]
    [InlineData(AqaraVibrationSensor.SensitivityState.Middle, 11)]
    [InlineData(AqaraVibrationSensor.SensitivityState.High, 1)]
    public void SetSensitivity_Sends_Write_Command(AqaraVibrationSensor.SensitivityState state, int value)
    {
        var sensor = CreateSensor();

        sensor.SetSensitivity(state);

        _mqttTransport.Verify(x => x.SendMessage(
            $"{{\"cmd\":\"write\",\"did\":\"{sensor.DeviceId}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}"), Times.Once);
    }
    [Theory]
    [InlineData(AqaraVibrationSensor.SensitivityState.Low, 21)]
    [InlineData(AqaraVibrationSensor.SensitivityState.Middle, 11)]
    [InlineData(AqaraVibrationSensor.SensitivityState.High, 1)]
    public async Task SetSensitivity_Sends_Write_Command_Async(AqaraVibrationSensor.SensitivityState state, int value)
    {
        var sensor = CreateSensor();

        await sensor.SetSensitivityAsync(state);

        _mqttTransport.Verify(x => x.SendMessage(
            $"{{\"cmd\":\"write\",\"did\":\"{sensor.DeviceId}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}"), Times.Once);
    }
}
