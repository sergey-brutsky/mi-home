using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeUnitTests.Devices.Mqtt;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class ProtocolValueIsolationTests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("zz")]
    [InlineData("0001")]
    public void Invalid_Ble_Value_Does_Not_Drop_Following_Temperature(string battery)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);
        var events = 0;
        sensor.OnBatteryPercentChangeAsync += _ => { events++; return Task.CompletedTask; };
        Action parse = () => sensor.ParseData("{\"evt\":[{\"eid\":4106,\"edata\":\"" + battery + "\"},{\"eid\":4100,\"edata\":\"fa00\"}]}");

        parse.Should().NotThrow();

        events.Should().Be(0);
        sensor.BatteryPercent.Should().Be(0);
        sensor.Temperature.Should().Be(25f);
        sensor.ToString().Should().Contain("Battery Percent: Unknown");
        CheckWarning("LumiBle", "4106");
    }

    [Theory]
    [InlineData("\"bad\"")]
    [InlineData("2147483648")]
    public void Invalid_Zigbee_Number_Does_Not_Change_State_Or_Drop_Humidity(string raw)
    {
        var sensor = CreateLumiProtocolDevice<XiaomiTemperatureHumiditySensor>(XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL);
        sensor.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2000}]");
        var events = 0;
        sensor.OnTemperatureChangeAsync += _ => { events++; return Task.CompletedTask; };
        Action parse = () => sensor.ParseData("[{\"res_name\":\"0.1.85\",\"value\":" + raw + "},{\"res_name\":\"0.2.85\",\"value\":4500}]");

        parse.Should().NotThrow();

        sensor.Temperature.Should().Be(20f);
        sensor.Humidity.Should().Be(45f);
        events.Should().Be(0);
        CheckWarning("LumiMqtt", "0.1.85");
    }

    [Fact]
    public void Invalid_Mesh_Boolean_Does_Not_Drop_The_Next_Property()
    {
        ProtocolDeviceRegistry.TryCreateMesh(XiaomiMeshWallSwitchSingleOpen.PDID, "mesh", _loggerFactory, out var device).Should().BeTrue();
        using var sensor = (XiaomiMeshWallSwitchSingleOpen)device;
        Action parse = () => sensor.ParseData("[{\"siid\":2,\"piid\":1,\"value\":\"bad\"},{\"siid\":10,\"piid\":1,\"value\":true}]");
        parse.Should().NotThrow();
        sensor.IsTurnedOn.Should().BeFalse();
        sensor.IsIndicatorLightEnabled.Should().BeTrue();
        CheckWarning("LumiMesh", "mi:2:1");
    }

    [Theory]
    [InlineData("null-reference")]
    [InlineData("invalid-operation")]
    public void Programming_Errors_In_A_Handler_Are_Not_Suppressed(string kind)
    {
        Exception failure = kind == "null-reference" ? new NullReferenceException("code defect") : new InvalidOperationException("code defect");
        using var device = new BugDevice(_loggerFactory, failure);
        device.InitializeProtocol(DeviceProtocol.LumiMqtt);
        Action parse = () => device.ParseData("[{\"res_name\":\"bad\",\"value\":1},{\"res_name\":\"good\",\"value\":2}]");
        parse.Should().Throw<Exception>().Which.Should().BeSameAs(failure);
        device.FollowingCalls.Should().Be(0);
        _loggerFactory.Entries.Should().NotContain(entry => entry.Level == LogLevel.Warning);
    }

    private void CheckWarning(string protocol, string field) => _loggerFactory.Entries.Should().Contain(entry =>
        entry.Level == LogLevel.Warning && entry.Message.Contains(protocol) && entry.Message.Contains(field));

    private sealed class BugDevice(ILoggerFactory logs, Exception failure) : ProtocolMappedDeviceBase(logs)
    {
        public int FollowingCalls { get; private set; }
        public override string DeviceId => "bug";
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
        {
            map.LumiMqtt("bad", _ => throw failure);
            map.LumiMqtt("good", _ => FollowingCalls++);
        }
    }
}
