using MiHomeLib.Protocols;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiHoneywellSmokeDetectorTests: MqttDeviceTestBase
{
    private XiaomiHoneywellSmokeDetector CreateSensor(bool withTransport = true)
    {
        var sensor = CreateLumiProtocolDevice<XiaomiHoneywellSmokeDetector>(XiaomiHoneywellSmokeDetector.LUMI_MQTT_MODEL);
        if (withTransport)
            ((IProtocolMappedManageableDevice)sensor).SetMqttTransport(_mqttTransport.Object);
        return sensor;
    }

    [Fact]
    public void Initial_Properties_Are_Density_Lqi_And_Chip_Temperature()
    {
        var sensor = CreateSensor();
        sensor.GetInitialPropertyNames().Should().Equal("density", "lqi", "chip_temperature");

        sensor.SetInitialProperties([JsonValue.Create(3), JsonValue.Create(150), JsonValue.Create(30)]);

        sensor.SmokeDensity.Should().Be(3);
        sensor.LinkQuality.Should().Be(150);
        sensor.ChipTemperature.Should().Be(30);
    }

    [Fact]
    public void Check_OnSmokeDensityChange_Event_Passes_Old_Value()
    {
        var sensor = CreateSensor();
        sensor.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2}]");
        int? oldValue = null;
        sensor.OnSmokeDensityChangeAsync += x => { oldValue = x; sensor.SmokeDensity.Should().Be(15); return Task.CompletedTask; };

        sensor.ParseData("[{\"res_name\":\"0.1.85\",\"value\":15}]");

        oldValue.Should().Be(2);
    }

    [Theory]
    [InlineData(1, true, "alarm")]
    [InlineData(0, false, "stopped")]
    public void Smoke_Report_Raises_Alarm_Events(int value, bool expectedAlarm, string expectedEvent)
    {
        var sensor = CreateSensor();
        string raised = null;
        sensor.OnAlarmAsync += () => { raised = "alarm"; return Task.CompletedTask; };
        sensor.OnAlarmStoppedAsync += () => { raised = "stopped"; return Task.CompletedTask; };

        sensor.ParseData($"[{{\"res_name\":\"13.1.85\",\"value\":{value}}}]");

        sensor.Alarm.Should().Be(expectedAlarm);
        raised.Should().Be(expectedEvent);
    }

    [Theory]
    [InlineData(67174400)]
    [InlineData(67239936)]
    [InlineData(67305472)]
    public void Check_OnSmokeSensivityModeChanged_Event(int value)
    {
        var sensor = CreateSensor();
        XiaomiHoneywellSmokeDetector.SensivityMode? mode = null;
        sensor.OnSmokeSensivityModeChanged += x => { mode = x; return Task.CompletedTask; };

        sensor.ParseData($"[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]");

        mode.Should().Be((XiaomiHoneywellSmokeDetector.SensivityMode)value);
    }

    [Fact]
    public void Voltage_Report_Is_Taken()
    {
        var sensor = CreateSensor();

        sensor.ParseData("[{\"res_name\":\"8.0.2008\",\"value\":3065}]");

        sensor.Voltage.Should().Be(3.065f);
    }

    [Theory]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.NoSmoke, 67174400)]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke, 67239936)]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.MiddleSmoke, 67305472)]
    public void Check_SetSensivity_Works(XiaomiHoneywellSmokeDetector.SensivityMode mode, int value)
    {
        var sensor = CreateSensor();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{sensor.DeviceId}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}";

        sensor.SetSensivity(mode);

        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Check_RunSelfTest_Works()
    {
        var sensor = CreateSensor();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{sensor.DeviceId}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{XiaomiHoneywellSmokeDetector.SELF_TEST_MAGIC_NUMBER}}}]}}";

        sensor.RunSelfTest();

        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Commands_Without_Mqtt_Transport_Throw_NotSupported()
    {
        var sensor = CreateSensor(withTransport: false);

        sensor.Invoking(x => x.RunSelfTest()).Should().Throw<NotSupportedException>().WithMessage("*Lumi MQTT gateways*");
    }
    [Theory]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.NoSmoke, 67174400)]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke, 67239936)]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.MiddleSmoke, 67305472)]
    public async Task Check_SetSensivity_Works_Async(XiaomiHoneywellSmokeDetector.SensivityMode mode, int value)
    {
        var sensor = CreateSensor();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{sensor.DeviceId}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}";

        await sensor.SetSensivityAsync(mode);

        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
