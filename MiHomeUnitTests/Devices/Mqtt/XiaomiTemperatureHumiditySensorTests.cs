using MiHomeUnitTests.Infrastructure;
using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;

namespace MiHomeUnitTests.Devices.Mqtt;

/// <summary>
/// Value change events pass the previous value, the new one is already in the property
/// </summary>
public class XiaomiTemperatureHumiditySensorTests: ProtocolDeviceTestBase
{
    private XiaomiTemperatureHumiditySensor CreateSensor() =>
        CreateLumiProtocolDevice<XiaomiTemperatureHumiditySensor>(XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL);

    [Fact]
    public void Check_OnTemperatureChange_Event()
    {
        // Arrange
        var th = CreateSensor();
        th.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2430}]");

        var changes = new ValueChangeRecorder<float>(() => th.Temperature);
        th.OnTemperatureChangeAsync += changes.Handle;

        // Act
        th.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2515}]");

        // Assert
        changes.Changes.Should().Equal((24.3f, 25.15f));
    }

    [Fact]
    public void Check_OnHumidityChange_Event()
    {
        // Arrange
        var th = CreateSensor();
        th.ParseData("[{\"res_name\":\"0.2.85\",\"value\":5010}]");

        var changes = new ValueChangeRecorder<float>(() => th.Humidity);
        th.OnHumidityChangeAsync += changes.Handle;

        // Act
        th.ParseData("[{\"res_name\":\"0.2.85\",\"value\":4343}]");

        // Assert
        changes.Changes.Should().Equal((50.1f, 43.43f));
    }
}
