using System;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraTemperatureHumiditySensorTests : ProtocolDeviceTestBase
{
    private static string Resource(string name, int value) => $"[{{\"res_name\":\"{name}\",\"value\":{value}}}]";

    // The event passes the previous value, the property already holds the new one
    private void CheckOldValueIsPassed(
        string resource, int firstRaw, int secondRaw, float first, float second,
        Action<AqaraTemperatureHumiditySensor, Func<float, Task>> subscribe,
        Func<AqaraTemperatureHumiditySensor, float> property)
    {
        var sensor = CreateSensor();
        sensor.ParseData(Resource(resource, firstRaw));

        float? eventValue = null;
        subscribe(sensor, value =>
        {
            eventValue = value;
            property(sensor).Should().Be(second);
            return Task.CompletedTask;
        });

        sensor.ParseData(Resource(resource, secondRaw));

        eventValue.Should().Be(first);
        property(sensor).Should().Be(second);
    }

    [Fact]
    public void Check_OnTemperatureChange_Event() =>
        CheckOldValueIsPassed("0.1.85", 2515, 2430, 25.15f, 24.3f, (s, h) => s.OnTemperatureChangeAsync += h, s => s.Temperature);

    [Fact]
    public void Check_OnHumidityChange_Event() =>
        CheckOldValueIsPassed("0.2.85", 4343, 5010, 43.43f, 50.1f, (s, h) => s.OnHumidityChangeAsync += h, s => s.Humidity);

    [Fact]
    public void Check_OnPressureChange_Event() =>
        CheckOldValueIsPassed("0.3.85", 99120, 99200, 991.2f, 992f, (s, h) => s.OnPressureChangeAsync += h, s => s.Pressure);

    private AqaraTemperatureHumiditySensor CreateSensor() =>
        CreateLumiProtocolDevice<AqaraTemperatureHumiditySensor>(
            AqaraTemperatureHumiditySensor.LUMI_MQTT_MODEL);
}
