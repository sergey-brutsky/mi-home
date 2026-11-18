using MiHomeLib.Protocols;
using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;
using System.Text.Json.Nodes;

namespace MiHomeUnitTests.Devices.Mqtt;
public class XiaomiBluetoothHygrothermograph3Tests : ProtocolDeviceTestBase
{
    private readonly XiaomiBluetoothHygrothermograph3 _th;

    public XiaomiBluetoothHygrothermograph3Tests() => _th = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph3>(XiaomiBluetoothHygrothermograph3.PDID);

    private static string Property(int siid, int piid, object value) =>
        new JsonObject { ["siid"] = siid, ["piid"] = piid, ["value"] = JsonValue.Create(value) }.ToString();

    [Fact]
    public void Check_OnTemperatureChange_Event()
    {
        float? oldValue = null;
        _th.ParseData(Property(3, 1001, 28.1f));
        _th.OnTemperatureChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        _th.ParseData(Property(3, 1001, 28.79f));

        oldValue.Should().Be(28.1f);
        _th.Temperature.Should().Be(28.79f);
    }

    [Fact]
    public void Check_OnHumidityChange_Event()
    {
        byte? oldValue = null;
        _th.ParseData(Property(3, 1002, 66));
        _th.OnHumidityChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        _th.ParseData(Property(3, 1002, (byte)55));

        oldValue.Should().Be(66);
        _th.Humidity.Should().Be(55);
    }

    [Fact]
    public void Check_OnBatteryPercentChange_Event()
    {
        byte? oldValue = null;
        _th.ParseData(Property(2, 1003, 20));
        _th.OnBatteryPercentChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

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
        _th.OnBatteryPercentChangeAsync += _ => { eventRaised = true; return Task.CompletedTask; };

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
    [Theory]
    [InlineData(3, 1001, "-12.5", -12.5f)]
    [InlineData(3, 1002, "55", 55f)]
    [InlineData(2, 1003, "80", 80f)]
    public void String_Values_Are_Parsed_And_Repeated_Reports_Pass_Previous_Value(int siid, int piid, string value, float expected)
    {
        var previous = new System.Collections.Generic.List<float>();
        if (piid == 1001) _th.OnTemperatureChangeAsync += old => { _th.Temperature.Should().Be(expected); previous.Add(old); return Task.CompletedTask; };
        else if (piid == 1002) _th.OnHumidityChangeAsync += old => { _th.Humidity.Should().Be((byte)expected); previous.Add(old); return Task.CompletedTask; };
        else _th.OnBatteryPercentChangeAsync += old => { _th.BatteryPercent.Should().Be((byte)expected); previous.Add(old); return Task.CompletedTask; };
        _th.ParseData(Property(siid, piid, value));
        _th.ParseData(Property(siid, piid, value));
        previous.Should().Equal(0, expected);
    }

    [Theory]
    [InlineData(3, 1001, "invalid")]
    [InlineData(3, 1002, "invalid")]
    [InlineData(3, 1002, "256")]
    [InlineData(3, 1002, "-1")]
    [InlineData(2, 1003, "invalid")]
    [InlineData(2, 1003, "256")]
    public void Invalid_Value_Does_Not_Change_State_Or_Raise_Event(int siid, int piid, string value)
    {
        _th.ParseData(Property(3, 1001, 21.5));
        _th.ParseData(Property(3, 1002, 50));
        _th.ParseData(Property(2, 1003, 90));
        var count = 0;
        _th.OnTemperatureChangeAsync += _ => { count++; return Task.CompletedTask; };
        _th.OnHumidityChangeAsync += _ => { count++; return Task.CompletedTask; };
        _th.OnBatteryPercentChangeAsync += _ => { count++; return Task.CompletedTask; };
        _th.ParseData(Property(siid, piid, value));
        count.Should().Be(0);
        _th.Temperature.Should().Be(21.5f);
        _th.Humidity.Should().Be(50);
        _th.BatteryPercent.Should().Be(90);
    }

    [Fact]
    public void Array_Reports_Update_All_Properties_And_Description_Includes_Mac()
    {
        ((MiHomeLib.Protocols.IProtocolMappedBleDevice)_th).SetMacAddress("aa:bb:cc:dd:ee:ff");
        _th.ParseData("[" + Property(3, 1001, -5.25) + "," + Property(3, 1002, 65) + "," + Property(2, 1003, 85) + "]");
        _th.Temperature.Should().Be(-5.25f);
        _th.Humidity.Should().Be(65);
        _th.BatteryPercent.Should().Be(85);
        _th.GetInitialPropertyNames().Should().BeEmpty();
        _th.ToString().Should().Contain(_th.DeviceId).And.Contain(_th.Mac)
            .And.Contain(XiaomiBluetoothHygrothermograph3.MODEL).And.Contain(XiaomiBluetoothHygrothermograph3.MARKET_MODEL);
    }
}
