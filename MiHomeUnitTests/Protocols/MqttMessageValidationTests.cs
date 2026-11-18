using MiHomeUnitTests.Devices.Mqtt;
using MiHomeLib.MqttGateway.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class MqttMessageValidationTests : MqttDeviceTestBase
{
    private readonly MqttGatewayBase _gateway;
    private const string ValidMeshReport = """{"method":"properties_changed","params":[{"did":"mesh","siid":2,"piid":1,"value":true}]}""";

    public MqttMessageValidationTests()
    {
        _devicesDiscoverer.Setup(x => x.DiscoverZigBeeDevices()).Returns([
            new ZigBeeDeviceInfo("zigbee", XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL),
            new ZigBeeDeviceInfo("cube", AqaraCubeSensor.LUMI_MQTT_MODEL)]);
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(x => x.Contains("get_device_prop"))))
            .ReturnsAsync("""{"code":0,"result":[2150,4500,90]}""");
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(x => x.Contains("get_device_prop") && x.Contains("cube"))))
            .ReturnsAsync("""{"code":0,"result":[3000,90]}""");
        _devicesDiscoverer.Setup(x => x.DiscoverBleDevices()).Returns([
            new BleDeviceInfo("ble", XiaomiBluetoothHygrothermograph2.PDID, "79e0fe38c1a4")]);
        _devicesDiscoverer.Setup(x => x.DiscoverMeshDevices()).Returns([
            new MeshDeviceInfo("mesh", XiaomiMeshWallSwitchSingleOpen.PDID, "AA:BB:CC:DD:EE:FF")]);
        _gateway = new Mock<MqttGatewayBase>("gateway", _miioTransport.Object, _mqttTransport.Object,
            _devicesDiscoverer.Object, _loggerFactory) { CallBase = true }.Object;
        _gateway.DiscoverDevices();
    }

    public static TheoryData<string, string> MalformedMessages => new()
    {
        { "zigbee/send", "{" },
        { "zigbee/send", "null" },
        { "zigbee/send", "[]" },
        { "zigbee/send", "{}" },
        { "zigbee/send", """{"cmd":null}""" },
        { "central/report", """{"method":42}""" },
        { "zigbee/send", """{"cmd":"report","params":[]}""" },
        { "zigbee/send", """{"cmd":"report","did":123,"time":1,"params":[]}""" },
        { "zigbee/send", """{"cmd":"report","did":"zigbee","params":[]}""" },
        { "zigbee/send", """{"cmd":"report","did":"zigbee","time":1e100,"params":[]}""" },
        { "zigbee/send", """{"cmd":"report","did":"zigbee","time":1,"params":{}}""" },
        { "zigbee/send", """{"cmd":"report","did":"zigbee","time":1,"params":[null]}""" },
        { "zigbee/send", """{"cmd":"report","did":"zigbee","time":1,"params":[{"res_name":"8.0.2001","value":"bad"}]}""" },
        { "zigbee/send", """{"cmd":"heartbeat","params":null}""" },
        { "zigbee/send", """{"cmd":"heartbeat","params":{}}""" },
        { "zigbee/send", """{"cmd":"heartbeat","params":[null]}""" },
        { "zigbee/send", """{"cmd":"heartbeat","params":[{"did":"zigbee","time":"bad","res_list":[]}]}""" },
        { "zigbee/send", """{"cmd":"read_rsp","did":"zigbee","results":{}}""" },
        { "zigbee/send", """{"cmd":"read_rsp","did":"zigbee","results":[42]}""" },
        { "central/report", """{"method":"event_occured","params":null}""" },
        { "central/report", """{"method":"event_occured","params":[]}""" },
        { "central/report", """{"method":"event_occured","params":{"did":"mesh","siid":"bad","eiid":1}}""" },
        { "central/report", """{"method":"properties_changed","params":{}}""" },
        { "central/report", """{"method":"properties_changed","params":[null]}""" },
        { "central/report", """{"method":"_async.ble_event","params":null}""" },
        { "central/report", """{"method":"_async.ble_event","params":{"dev":[]}}""" },
        { "central/report", """{"method":"_async.ble_event","params":{"dev":{"did":null},"gwts":1,"evt":[]}}""" },
        { "central/report", """{"method":"_async.ble_event","params":{"dev":{"did":"ble"},"evt":[]}}""" },
        { "central/report", """{"method":"_async.ble_event","params":{"dev":{"did":"ble"},"gwts":1,"evt":[null]}}""" },
        { "central/report", """{"method":"_async.ble_event","params":{"dev":{"did":"ble"},"gwts":1,"evt":[{"eid":4106,"edata":null}]}}""" },
        { "central/report", """{"method":"_async.ble_event","params":{"dev":{"did":"ble"},"gwts":1,"evt":[{"eid":4106,"edata":"zz"}]}}""" },
    };

    [Theory]
    [MemberData(nameof(MalformedMessages))]
    public void Invalid_Message_Is_Logged_And_Next_Message_Is_Processed(string topic, string json)
    {
        var warningsBefore = _loggerFactory.Entries.Count(x => x.Level == LogLevel.Warning);
        Action receive = () => Receive(topic, json);

        receive.Should().NotThrow();
        _loggerFactory.Entries.Count(x => x.Level == LogLevel.Warning).Should().BeGreaterThan(warningsBefore);
        Receive("central/report", ValidMeshReport);
        _gateway.GetDeviceByDid<XiaomiMeshWallSwitchSingleOpen>("mesh").IsTurnedOn.Should().BeTrue();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("""{"did":null}""")]
    [InlineData("""{"did":"mesh","siid":2,"piid":1,"value":"bad"}""")]
    [InlineData("""{"did":"mesh","siid":2,"piid":1}""")]
    public void Invalid_Property_Does_Not_Drop_Later_Items(string badItem)
    {
        var report = """{"method":"properties_changed","params":[""" + badItem +
            """,{"did":"mesh","siid":2,"piid":1,"value":true}]}""";
        Action receive = () => Receive("central/report", report);

        receive.Should().NotThrow();
        _gateway.GetDeviceByDid<XiaomiMeshWallSwitchSingleOpen>("mesh").IsTurnedOn.Should().BeTrue();
        _loggerFactory.Entries.Should().Contain(x => x.Level == LogLevel.Warning);
    }

    [Fact]
    public void Invalid_Read_Result_Does_Not_Drop_Valid_Resources()
    {
        Receive("zigbee/send", """{"cmd":"read_rsp","did":"zigbee","results":[null,42,{"res_name":"bad","error_code":"bad"},{"res_name":"8.0.2001","value":75}]}""");
        _gateway.GetDeviceByDid<XiaomiTemperatureHumiditySensor>("zigbee").BatteryPercent.Should().Be(75);
    }

    [Theory]
    [InlineData("report", "params")]
    [InlineData("read_rsp", "results")]
    public void Cube_Combines_Resources_Once_Per_Message(string command, string field)
    {
        var cube = _gateway.GetDeviceByDid<AqaraCubeSensor>("cube");
        var rotations = new List<AqaraCubeSensor.CubeRotate>();
        cube.OnStatusChangedAsync += _ =>
        {
            rotations.Add(cube.Rotate);
            return Task.CompletedTask;
        };

        Receive("zigbee/send", $$"""{"cmd":"{{command}}","did":"cube","time":1700000000123,"{{field}}":[{"res_name":"13.1.85","value":16},{"res_name":"0.3.85","value":-45300},{"res_name":"0.2.85","value":500}]}""");

        _loggerFactory.Entries.Should().NotContain(x => x.Level >= LogLevel.Warning);
        rotations.Should().ContainSingle();
        rotations[0].Shift.Should().Be(-45);
        rotations[0].Value.Should().Be(500);
    }

    [Fact]
    public async Task Async_Handler_Can_Read_State_After_Message_Document_Is_Disposed()
    {
        var mesh = _gateway.GetDeviceByDid<XiaomiMeshWallSwitchSingleOpen>("mesh");
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task handler = null;
        bool? previous = null;
        bool? current = null;
        async Task Handle(bool oldValue)
        {
            await resume.Task;
            previous = oldValue;
            current = mesh.IsTurnedOn;
        }
        mesh.OnStateChangeAsync += oldValue => handler = Handle(oldValue);

        Receive("central/report", ValidMeshReport);
        handler.Should().NotBeNull();
        handler.IsCompleted.Should().BeFalse();
        resume.SetResult(true);
        await handler;

        previous.Should().BeFalse();
        current.Should().BeTrue();
    }

    private void Receive(string topic, string json) =>
        _mqttTransport.Raise(x => x.OnMessageReceived += null, topic, json);

    [Theory]
    [InlineData("null")]
    [InlineData("{\"eid\":4106}")]
    [InlineData("{\"eid\":4106,\"edata\":\"zz\"}")]
    [InlineData("{\"eid\":4106,\"edata\":\"0001\"}")]
    public void Invalid_Ble_Event_Does_Not_Drop_Following_Battery_Report(string invalid)
    {
        Receive("central/report", "{\"method\":\"_async.ble_event\",\"params\":{\"dev\":{\"did\":\"ble\"},\"gwts\":1700000000,\"evt\":[" + invalid + ",{\"eid\":4106,\"edata\":\"4b\"}]}}");
        _gateway.GetDeviceByDid<XiaomiBluetoothHygrothermograph2>("ble").BatteryPercent.Should().Be(75);
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("LumiBle"));
    }
}
