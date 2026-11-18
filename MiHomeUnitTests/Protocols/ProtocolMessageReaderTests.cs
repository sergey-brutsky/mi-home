using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeUnitTests.Devices.Mqtt;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class ProtocolMessageReaderTests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("null", "object")]
    [InlineData("42", "object")]
    [InlineData("{}", "siid")]
    [InlineData("{\"res_name\":null,\"value\":1}", "res_name")]
    [InlineData("{\"res_name\":17,\"value\":1}", "res_name")]
    [InlineData("{\"res_name\":\"\",\"value\":1}", "res_name")]
    [InlineData("{\"res_name\":\"bad\"}", "value")]
    [InlineData("{\"siid\":\"2\",\"piid\":1,\"value\":1}", "siid")]
    [InlineData("{\"siid\":2,\"piid\":null,\"value\":1}", "piid")]
    [InlineData("{\"siid\":2,\"eiid\":true}", "eiid")]
    public void Malformed_Lumi_Item_Does_Not_Drop_Following_Resources(string invalid, string reason)
    {
        using var document = JsonDocument.Parse("[" + invalid + ", {\"res_name\":\"8.0.2001\",\"value\":75}]");

        var entries = ProtocolMessageReader.Read(DeviceProtocol.LumiMqtt, false, document.RootElement, _loggerFactory.CreateLogger("test"))
            .Where(entry => entry.Key is not null).ToArray();

        entries.Should().ContainSingle().Which.Key.Should().Be("8.0.2001");
        entries[0].Value.GetInt32().Should().Be(75);
        CheckWarning("LumiMqtt", reason);
    }

    [Theory]
    [InlineData("LumiMesh")]
    [InlineData("LumiBle")]
    public void MiSpec_Diagnostics_Name_The_Actual_Protocol_And_Missing_Field(string protocolName)
    {
        var protocol = Enum.Parse<DeviceProtocol>(protocolName);
        using var document = JsonDocument.Parse("[{\"value\":1},{\"siid\":2,\"piid\":1,\"value\":true}]");
        var entries = ProtocolMessageReader.Read(protocol, true, document.RootElement, _loggerFactory.CreateLogger("test"))
            .Where(entry => entry.Key is not null).ToArray();
        entries.Should().ContainSingle().Which.Key.Should().Be("mi:2:1");
        CheckWarning(protocolName, "siid");
    }

    [Theory]
    [InlineData("null", "object")]
    [InlineData("{\"eid\":\"4106\",\"edata\":\"64\"}", "eid")]
    [InlineData("{\"eid\":4106}", "edata")]
    [InlineData("{\"eid\":4106,\"edata\":null}", "edata")]
    public void Malformed_MiBeacon_Item_Does_Not_Drop_The_Next_Event(string invalid, string reason)
    {
        using var document = JsonDocument.Parse("{\"evt\":[" + invalid + ",{\"eid\":4106,\"edata\":\"64\"}]}");
        var entries = ProtocolMessageReader.Read(DeviceProtocol.LumiBle, false, document.RootElement, _loggerFactory.CreateLogger("test"))
            .Where(entry => entry.Key is not null).ToArray();
        entries.Should().ContainSingle().Which.Key.Should().Be("4106");
        entries[0].Value.GetString().Should().Be("64");
        CheckWarning("LumiBle", reason);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("17")]
    public void Invalid_Gateway2_Root_Is_Logged(string json)
    {
        using var document = JsonDocument.Parse(json);
        ProtocolMessageReader.Read(DeviceProtocol.XiaomiGateway2, false, document.RootElement, _loggerFactory.CreateLogger("test"))
            .Where(entry => entry.Key is not null).Should().BeEmpty();
        CheckWarning("XiaomiGateway2", "object");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"res_name\":\"0.2.85\"}")]
    public void Damaged_Cube_Report_Updates_Independent_Properties_Without_Emitting_A_Partial_Rotation(string invalid)
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(AqaraCubeSensor.LUMI_MQTT_MODEL);
        cube.ParseData("[{\"res_name\":\"13.1.85\",\"value\":16},{\"res_name\":\"0.3.85\",\"value\":-45000},{\"res_name\":\"0.2.85\",\"value\":500}]");
        var rotations = new List<AqaraCubeSensor.CubeRotate>();
        cube.OnStatusChangedAsync += _ => { rotations.Add(cube.Rotate); return Task.CompletedTask; };

        cube.ParseData("[{\"res_name\":\"13.1.85\",\"value\":16},{\"res_name\":\"0.3.85\",\"value\":90000}," + invalid + ",{\"res_name\":\"8.0.2008\",\"value\":2995}]");

        rotations.Should().BeEmpty();
        cube.Rotate.Shift.Should().Be(-45);
        cube.Rotate.Value.Should().Be(500);
        cube.Voltage.Should().Be(2.995f);
        cube.ParseData("[{\"res_name\":\"8.0.2008\",\"value\":3000}]");
        rotations.Should().BeEmpty();
        cube.ParseData("[{\"res_name\":\"13.1.85\",\"value\":16},{\"res_name\":\"0.3.85\",\"value\":10000},{\"res_name\":\"0.2.85\",\"value\":100}]");
        rotations.Should().ContainSingle();
        rotations[0].Shift.Should().Be(10);
        rotations[0].Value.Should().Be(100);
    }

    private void CheckWarning(string protocol, string reason) => _loggerFactory.Entries.Should().Contain(entry =>
        entry.Level == LogLevel.Warning && entry.Message.Contains(protocol) && entry.Message.Contains(reason));
}
