using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeUnitTests.Devices.Mqtt;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class ProtocolNumericValidationTests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("8.0.2001", -1)]
    [InlineData("8.0.2001", 256)]
    [InlineData("8.0.2007", -1)]
    [InlineData("8.0.2007", 256)]
    [InlineData("8.0.2006", -1)]
    [InlineData("8.0.2006", 256)]
    public void Byte_Overflow_Preserves_State_And_Processes_The_Next_Property(string resource, int number)
    {
        var sensor = CreateLumiProtocolDevice<XiaomiTemperatureHumiditySensor>(XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL);
        sensor.ParseData("[{\"res_name\":\"" + resource + "\",\"value\":42}]");
        var events = 0;
        sensor.OnBatteryPercentChangeAsync += _ => { events++; return Task.CompletedTask; };
        sensor.OnLinkQualityChangeAsync += _ => { events++; return Task.CompletedTask; };
        sensor.OnChipTemperatureChangeAsync += _ => { events++; return Task.CompletedTask; };

        sensor.ParseData("[{\"res_name\":\"" + resource + "\",\"value\":" + number + "},{\"res_name\":\"0.2.85\",\"value\":4500}]");

        ReadByte(sensor, resource).Should().Be(42);
        events.Should().Be(0);
        sensor.Humidity.Should().Be(45);
        CheckWarning(resource);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Byte_Type_Boundaries_Are_Accepted(int number)
    {
        var sensor = CreateLumiProtocolDevice<XiaomiTemperatureHumiditySensor>(XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL);
        sensor.ParseData("[{\"res_name\":\"8.0.2001\",\"value\":" + number + "}]");
        sensor.BatteryPercent.Should().Be((byte)number);
        _loggerFactory.Entries.Should().NotContain(x => x.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData("battery", "8.0.2001", -1)]
    [InlineData("battery", "8.0.2001", 256)]
    [InlineData("lqi", "8.0.2007", -1)]
    [InlineData("lqi", "8.0.2007", 256)]
    [InlineData("chip_temperature", "8.0.2006", -1)]
    [InlineData("chip_temperature", "8.0.2006", 256)]
    public void Initial_Byte_Overflow_Does_Not_Overwrite_The_Previous_Value(string property, string resource, int number)
    {
        var sensor = CreateLumiProtocolDevice<AqaraTemperatureHumiditySensor>(AqaraTemperatureHumiditySensor.LUMI_MQTT_MODEL);
        sensor.ParseData("[{\"res_name\":\"" + resource + "\",\"value\":42}]");
        var values = sensor.GetInitialPropertyNames().Select(name => JsonValue.Create(name == property ? number : 0) as JsonNode).ToArray();
        Action initialize = () => sensor.SetInitialProperties(values);

        initialize.Should().Throw<OverflowException>();
        ReadByte(sensor, resource).Should().Be(42);
    }

    [Theory]
    [InlineData("1e400")]
    [InlineData("-1e400")]
    public void Double_Overflow_Is_Rejected(string raw)
    {
        using var json = JsonDocument.Parse(raw);
        Action read = () => json.RootElement.ReadDouble();
        read.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("1e400")]
    [InlineData("-1e400")]
    [InlineData("1e39")]
    [InlineData("-1e39")]
    public void Invalid_Single_Preserves_State_And_Processes_The_Next_Property(string raw)
    {
        var relay = CreateLumiProtocolDevice<AqaraT1WithNeutral>(AqaraT1WithNeutral.LUMI_MQTT_MODEL);
        relay.ParseData("[{\"siid\":3,\"piid\":2,\"value\":12.5}]");
        var events = 0;
        relay.OnLoadPowerChangeAsync += _ => { events++; return Task.CompletedTask; };

        relay.ParseData("[{\"siid\":3,\"piid\":2,\"value\":" + raw + "},{\"siid\":2,\"piid\":1,\"value\":true}]");

        relay.LoadPower.Should().Be(12.5f);
        events.Should().Be(0);
        relay.State.Should().Be(AqaraT1WithNeutral.RelayState.On);
        CheckWarning("mi:3:2");
    }

    [Theory]
    [InlineData("1e400")]
    [InlineData("1e39")]
    [InlineData("\"1e400\"")]
    [InlineData("\"NaN\"")]
    [InlineData("\"Infinity\"")]
    [InlineData("\"-Infinity\"")]
    public void Invalid_Gateway2_Float_Preserves_State_And_Processes_The_Next_Property(string raw)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.XiaomiGateway2, XiaomiMiSmartPowerPlugCN.XIAOMI_GATEWAY2_MODEL,
            "plug", 0, _loggerFactory, out var created).Should().BeTrue();
        using var plug = (XiaomiMiSmartPowerPlugCN)created;
        plug.ParseData("{\"load_power\":\"12.5\"}");
        var events = 0;
        plug.OnLoadPowerChangeAsync += _ => { events++; return Task.CompletedTask; };

        plug.ParseData("{\"load_power\":" + raw + ",\"status\":\"on\"}");

        plug.LoadPower.Should().Be(12.5f);
        events.Should().Be(0);
        plug.State.Should().Be(XiaomiMiSmartPowerPlugCN.PlugState.On);
        _loggerFactory.Entries.Should().Contain(x => x.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData("1e400")]
    [InlineData("1e39")]
    public void Invalid_Initial_Single_Is_Rejected(string raw)
    {
        var plug = CreateLumiProtocolDevice<XiaomiMiSmartPowerPlugCN>(XiaomiMiSmartPowerPlugCN.LUMI_MQTT_MODEL);
        plug.ParseData("[{\"res_name\":\"0.12.85\",\"value\":12.5}]");
        var values = plug.GetInitialPropertyNames().Select(name => JsonNode.Parse(name switch
        {
            "neutral_0" => "\"off\"",
            "load_power" => raw,
            _ => "0"
        })).ToArray();
        Action initialize = () => plug.SetInitialProperties(values);

        initialize.Should().Throw<FormatException>();
        plug.LoadPower.Should().Be(12.5f);
    }

    [Fact]
    public void Cube_Angle_Is_Scaled_Before_Converting_To_Single()
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(AqaraCubeSensor.LUMI_MQTT_MODEL);
        // A finite scaled value is representable as float, although its raw value is not.
        cube.ParseData("[{\"res_name\":\"0.3.85\",\"value\":1e40}]");
        _loggerFactory.Entries.Should().NotContain(x => x.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData("1e50")]
    [InlineData("1e400")]
    public void Invalid_Cube_Angle_Does_Not_Emit_A_Rotation(string raw)
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(AqaraCubeSensor.LUMI_MQTT_MODEL);
        var events = 0;
        cube.OnStatusChangedAsync += _ => { events++; return Task.CompletedTask; };

        cube.ParseData("[{\"res_name\":\"13.1.85\",\"value\":16},{\"res_name\":\"0.3.85\",\"value\":" + raw +
            "},{\"res_name\":\"8.0.2008\",\"value\":3000}]");

        events.Should().Be(0);
        cube.Status.Should().Be(AqaraCubeSensor.CubeStatus.Undefined);
        cube.Voltage.Should().Be(3);
        CheckWarning("0.3.85");
    }

    [Theory]
    [InlineData("0", 0f)]
    [InlineData("-12.5", -12.5f)]
    [InlineData("3.4028234663852886e38", float.MaxValue)]
    [InlineData("-3.4028234663852886e38", float.MinValue)]
    public void Finite_Single_Values_Are_Accepted_In_Reports_And_Initial_Properties(string raw, float expected)
    {
        using var document = JsonDocument.Parse(raw);
        document.RootElement.ReadSingle().Should().Be(expected);
        JsonNode.Parse(raw).ReadSingle().Should().Be(expected);
    }

    private static byte ReadByte(LumiBatteryDeviceBase sensor, string resource) => resource switch
    {
        "8.0.2001" => sensor.BatteryPercent,
        "8.0.2007" => sensor.LinkQuality,
        _ => sensor.ChipTemperature
    };

    private void CheckWarning(string field) => _loggerFactory.Entries.Should().Contain(x =>
        x.Level == LogLevel.Warning && x.Message.Contains(field));
}
