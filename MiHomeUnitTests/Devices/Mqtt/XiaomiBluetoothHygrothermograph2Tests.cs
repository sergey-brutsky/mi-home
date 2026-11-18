using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiBluetoothHygrothermograph2Tests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("E500", 22.9f, "f100", 24.1f)]
    [InlineData("fb00", 25.1f, "dc00", 22f)]
    [InlineData("d400", 21.2f, "dc00", 22f)]
    [InlineData("cbff", -5.3f, "0a00", 1f)]
    public void Check_OnTemperatureChange_Event(string edata, float expectedTemperature, string oldData, float oldTemperature)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);
        sensor.ParseData(SetupBleAsyncEventParams(4100, oldData).ToString());
        float? reportedTemperature = null;
        sensor.OnTemperatureChangeAsync += value =>
        {
            reportedTemperature = value;
            sensor.Temperature.Should().Be(expectedTemperature);
            return Task.CompletedTask;
        };

        sensor.ParseData(SetupBleAsyncEventParams(4100, edata).ToString());

        reportedTemperature.Should().Be(oldTemperature);
    }

    [Theory]
    [InlineData("E601", 48.6f, "bc01", 44.4f)]
    [InlineData("e301", 48.3f, "bc01", 44.4f)]
    [InlineData("5102", 59.3f, "bc01", 44.4f)]
    [InlineData("5202", 59.4f, "bc01", 44.4f)]
    [InlineData("5302", 59.5f, "bc01", 44.4f)]
    public void Check_OnHumidityChange_Event(string edata, float expectedHumidity, string oldData, float oldHumidity)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);
        sensor.ParseData(SetupBleAsyncEventParams(4102, oldData).ToString());
        float? reportedHumidity = null;
        sensor.OnHumidityChangeAsync += value =>
        {
            reportedHumidity = value;
            sensor.Humidity.Should().Be(expectedHumidity);
            return Task.CompletedTask;
        };

        sensor.ParseData(SetupBleAsyncEventParams(4102, edata).ToString());

        reportedHumidity.Should().Be(oldHumidity);
    }
}
