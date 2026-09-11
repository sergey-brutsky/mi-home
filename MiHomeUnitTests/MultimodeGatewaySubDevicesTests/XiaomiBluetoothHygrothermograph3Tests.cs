using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
public class XiaomiBluetoothHygrothermograph3Tests : MqttGatewayDeviceTests
{
    private readonly XiaomiBluetoothHygrothermograph3 _th;

    public XiaomiBluetoothHygrothermograph3Tests() => _th = _fixture.Build<XiaomiBluetoothHygrothermograph3>().Create();

    [Theory]
    [InlineData(19457, "cdccc041", 24.1f, 20f)]
    [InlineData(19457, "0000b041", 22f, 18.5f)]
    [InlineData(19457, "9a99a9c0", -5.3f, 0f)]
    [InlineData(19457, "00000000", 0f, 12.7f)]
    public void Check_OnTemperatureChange_Event(int eid, string edata, float newTemperature, float oldTemperature)
    {
        // Arrange
        var eventRaised = false;

        _th.Temperature = oldTemperature;

        _th.OnTemperatureChangeAsync += (oldValue) =>
        {
            eventRaised = oldValue == oldTemperature;
            _th.Temperature.Should().Be(newTemperature);
            return Task.CompletedTask;
        };

        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(19458, "2d", 45, 33)]
    [InlineData(19458, "64", 100, 0)]
    [InlineData(19458, "00", 0, 55)]
    public void Check_OnHumidityChange_Event(int eid, string edata, byte newHumidity, byte oldHumidity)
    {
        // Arrange
        var eventRaised = false;

        _th.Humidity = oldHumidity;

        _th.OnHumidityChangeAsync += (oldValue) =>
        {
            eventRaised = oldValue == oldHumidity;
            _th.Humidity.Should().Be(newHumidity);
            return Task.CompletedTask;
        };

        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(18435, "57", 87, 90)]
    [InlineData(18435, "64", 100, 99)]
    public void Check_OnBatteryPercentChange_Event(int eid, string edata, byte newBatteryPercent, byte oldBatteryPercent)
    {
        // Arrange
        var eventRaised = false;

        _th.BatteryPercent = oldBatteryPercent;

        _th.OnBatteryPercentChangeAsync += (oldValue) =>
        {
            eventRaised = oldValue == oldBatteryPercent;
            _th.BatteryPercent.Should().Be(newBatteryPercent);
            return Task.CompletedTask;
        };

        // Act
        _th.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_Device_ToString()
    {
        // Arrange
        _th.ParseData(SetupBleAsyncEventParams(19457, "cdccc041").ToString());
        _th.ParseData(SetupBleAsyncEventParams(19458, "2d").ToString());
        _th.ParseData(SetupBleAsyncEventParams(18435, "57").ToString());

        // Act & Assert
        // ToString() uses the current culture, so the decimal separator is not hardcoded here
        _th.ToString().Should().Contain($"Temperature: {24.1f}°C")
            .And.Contain("Humidity: 45%")
            .And.Contain("Battery Percent: 87%");
    }

    [Fact]
    public void Check_Device_Model_And_Pdid()
    {
        XiaomiBluetoothHygrothermograph3.MODEL.Should().Be("miaomiaoce.sensor_ht.t9");
        XiaomiBluetoothHygrothermograph3.MARKET_MODEL.Should().Be("MJWSD05MMC");
        XiaomiBluetoothHygrothermograph3.PDID.Should().Be(10290);
    }
}
