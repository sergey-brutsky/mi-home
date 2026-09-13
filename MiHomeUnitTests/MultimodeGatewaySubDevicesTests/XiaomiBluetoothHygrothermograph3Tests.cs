using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway.Devices;
using System.Text.Json.Nodes;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
public class XiaomiBluetoothHygrothermograph3Tests : MqttGatewayDeviceTests
{
    private readonly XiaomiBluetoothHygrothermograph3 _th;

    public XiaomiBluetoothHygrothermograph3Tests() => _th = _fixture.Build<XiaomiBluetoothHygrothermograph3>().Create();

    [Theory]
    [InlineData(3, 1001, 28.79f, 28.1f)]
    public void Check_OnTemperatureChange_Event(int siid, int piid, float newTemperature, float oldTemperature)
    {
        // Arrange
        var eventRaised = false;
        
        JsonObject jsonNode = new()
        {
            ["siid"] = siid,
            ["piid"] = piid,
            ["value"] = newTemperature,
        };

        _th.Temperature = oldTemperature;

        _th.OnTemperatureChangeAsync += (oldValue) =>
        {
            eventRaised = oldValue == oldTemperature;
            _th.Temperature.Should().Be(newTemperature);
            return Task.CompletedTask;
        };

        // Act
        _th.ParseData(jsonNode.ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(3, 1002, 55, 66)]
    public void Check_OnHumidityChange_Event(int siid, int piid, byte newHumidity, byte oldHumidity)
    {
        // Arrange
        var eventRaised = false;
         JsonObject jsonNode = new()
        {
            ["siid"] = siid,
            ["piid"] = piid,
            ["value"] = newHumidity,
        };

        _th.Humidity = oldHumidity;

        _th.OnHumidityChangeAsync += (oldValue) =>
        {
            eventRaised = oldValue == oldHumidity;
            _th.Humidity.Should().Be(newHumidity);
            return Task.CompletedTask;
        };

        // Act
        _th.ParseData(jsonNode.ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(2, 1003, 10, 20)]
    public void Check_OnBatteryPercentChange_Event(int siid, int piid, byte newPercent, byte oldPercent)
    {
        // Arrange
        var eventRaised = false;
         JsonObject jsonNode = new()
        {
            ["siid"] = siid,
            ["piid"] = piid,
            ["value"] = newPercent,
        };

        _th.BatteryPercent = oldPercent;

        _th.OnBatteryPercentAsync += (oldValue) =>
        {
            eventRaised = oldValue == oldPercent;
            _th.BatteryPercent.Should().Be(newPercent);
            return Task.CompletedTask;
        };

        // Act
        _th.ParseData(jsonNode.ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_Device_Model_And_Pdid()
    {
        XiaomiBluetoothHygrothermograph3.MODEL.Should().Be("miaomiaoce.sensor_ht.t9");
        XiaomiBluetoothHygrothermograph3.MARKET_MODEL.Should().Be("MJWSD05MMC");
        XiaomiBluetoothHygrothermograph3.PDID.Should().Be(10290);
    }
}
