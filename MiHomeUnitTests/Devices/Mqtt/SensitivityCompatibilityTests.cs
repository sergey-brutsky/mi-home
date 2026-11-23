using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class SensitivityCompatibilityTests : MqttDeviceTestBase
{
#pragma warning disable CS0618 // Intentional coverage of the API retained for 3.x clients.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Smoke_Old_And_New_Methods_Send_The_Same_Command(bool legacy, bool async)
    {
        using var sensor = CreateLumiProtocolDevice<XiaomiHoneywellSmokeDetector>(XiaomiHoneywellSmokeDetector.LUMI_MQTT_MODEL);
        ((IProtocolMappedManageableDevice)sensor).SetMqttTransport(_mqttTransport.Object);
        if (legacy)
        {
            if (async) await sensor.SetSensivityAsync(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke);
            else sensor.SetSensivity(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke);
        }
        else if (async) await sensor.SetSensitivityAsync(XiaomiHoneywellSmokeDetector.SensitivityMode.LowSmoke);
        else sensor.SetSensitivity(XiaomiHoneywellSmokeDetector.SensitivityMode.LowSmoke);
        _mqttTransport.Verify(x => x.SendMessage("{\"cmd\":\"write\",\"did\":\"" + sensor.DeviceId + "\",\"params\":[{\"res_name\":\"14.1.85\",\"value\":67239936}]}"), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Vibration_Old_And_New_Methods_Send_The_Same_Command(bool legacy, bool async)
    {
        using var sensor = CreateLumiProtocolDevice<AqaraVibrationSensor>(AqaraVibrationSensor.LUMI_MQTT_MODEL);
        ((IProtocolMappedManageableDevice)sensor).SetMqttTransport(_mqttTransport.Object);
        if (legacy)
        {
            if (async) await sensor.SetSensivityAsync(AqaraVibrationSensor.SensivityState.Low);
            else sensor.SetSensivity(AqaraVibrationSensor.SensivityState.Low);
        }
        else if (async) await sensor.SetSensitivityAsync(AqaraVibrationSensor.SensitivityState.Low);
        else sensor.SetSensitivity(AqaraVibrationSensor.SensitivityState.Low);
        _mqttTransport.Verify(x => x.SendMessage("{\"cmd\":\"write\",\"did\":\"" + sensor.DeviceId + "\",\"params\":[{\"res_name\":\"14.1.85\",\"value\":21}]}"), Times.Once);
    }

    [Fact]
    public async Task Smoke_Raises_And_Awaits_New_Event_Before_Legacy_Event()
    {
        using var sensor = CreateLumiProtocolDevice<XiaomiHoneywellSmokeDetector>(XiaomiHoneywellSmokeDetector.LUMI_MQTT_MODEL);
        var order = new List<string>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sensor.OnSmokeSensitivityModeChangedAsync += async value =>
        {
            value.Should().Be(XiaomiHoneywellSmokeDetector.SensitivityMode.LowSmoke);
            order.Add("new");
            await release.Task;
        };
        sensor.OnSmokeSensivityModeChanged += value =>
        {
            value.Should().Be(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke);
            order.Add("old"); complete.TrySetResult(true); return Task.CompletedTask;
        };
        sensor.ParseData("[{\"res_name\":\"14.1.85\",\"value\":67239936}]");
        order.Should().Equal("new");
        release.SetResult(true);
        await complete.Task.WaitAsync(System.TimeSpan.FromSeconds(2));
        order.Should().Equal("new", "old");
    }

    [Theory]
    [InlineData(AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    [InlineData(AqaraOppleFourButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    public void Registry_Opple_Devices_Keep_Their_Legacy_Base_Type(string model)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, "test", 0, NullLoggerFactory.Instance, out var device).Should().BeTrue();
        using (device)
        {
            if (model == AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL)
            {
                device.Should().BeOfType<AqaraOppleTwoButtonsWirelessSwitch>();
                device.Should().BeAssignableTo<AqaraOppleTwoButtonsWirelesSwitch>();
            }
            else
            {
                device.Should().BeOfType<AqaraOppleFourButtonsWirelessSwitch>();
                device.Should().BeAssignableTo<AqaraOppleFourButtonsWirelesSwitch>();
            }
        }
    }
#pragma warning restore CS0618
}
