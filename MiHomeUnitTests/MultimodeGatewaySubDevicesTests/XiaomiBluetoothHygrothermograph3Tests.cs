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

    private static string Property(int siid, int piid, object value) =>
        new JsonObject { ["siid"] = siid, ["piid"] = piid, ["value"] = JsonValue.Create(value) }.ToString();

    [Fact]
    public void Check_OnTemperatureChange_Event()
    {
        float? oldValue = null;
        _th.Temperature = 28.1f;
        _th.OnTemperatureChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        _th.ParseData(Property(3, 1001, 28.79f));

        oldValue.Should().Be(28.1f);
        _th.Temperature.Should().Be(28.79f);
    }

    [Fact]
    public void Check_OnHumidityChange_Event()
    {
        byte? oldValue = null;
        _th.Humidity = 66;
        _th.OnHumidityChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        _th.ParseData(Property(3, 1002, (byte)55));

        oldValue.Should().Be(66);
        _th.Humidity.Should().Be(55);
    }

    [Fact]
    public void Check_OnBatteryPercentChange_Event()
    {
        byte? oldValue = null;
        _th.BatteryPercent = 20;
        _th.OnBatteryPercentAsync += x => { oldValue = x; return Task.CompletedTask; };

        _th.ParseData(Property(2, 1003, (byte)10));

        oldValue.Should().Be(20);
        _th.BatteryPercent.Should().Be(10);
    }

    [Theory]
    [InlineData(2, 1001)]
    [InlineData(3, 1003)]
    public void Check_Property_With_Wrong_Siid_Is_Ignored(int siid, int piid)
    {
        // Arrange: piid alone is not enough, the same piid under another service must not change anything
        var eventRaised = false;

        _th.OnTemperatureChangeAsync += _ => { eventRaised = true; return Task.CompletedTask; };
        _th.OnHumidityChangeAsync += _ => { eventRaised = true; return Task.CompletedTask; };
        _th.OnBatteryPercentAsync += _ => { eventRaised = true; return Task.CompletedTask; };

        // Act
        _th.ParseData(Property(siid, piid, 50));

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void Check_Ble_Event_Payload_Is_Ignored()
    {
        // Arrange: _async.ble_event params have evt/eid and no siid
        var eventRaised = false;
        _th.OnTemperatureChangeAsync += _ => { eventRaised = true; return Task.CompletedTask; };

        var bleEvent = SetupBleAsyncEventParams(4100, "d400").ToString();

        // Act
        var act = () => _th.ParseData(bleEvent);

        // Assert
        act.Should().NotThrow();
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void Check_Device_Model_And_Pdid()
    {
        XiaomiBluetoothHygrothermograph3.MODEL.Should().Be("miaomiaoce.sensor_ht.t9");
        XiaomiBluetoothHygrothermograph3.MARKET_MODEL.Should().Be("MJWSD05MMC");
        XiaomiBluetoothHygrothermograph3.PDID.Should().Be(10290);
    }
}
