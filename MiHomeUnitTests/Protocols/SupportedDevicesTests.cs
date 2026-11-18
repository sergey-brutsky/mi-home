using System;
using System.Linq;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class SupportedDevicesTests
{
    // Explicit compatibility contract. Update deliberately when adding or removing support.
    internal static readonly (DeviceProtocol Protocol, string Model, int Pdid, Type Type)[] Expected =
    [
        (DeviceProtocol.XiaomiGateway2, "sensor_cube.aqgl01", 0, typeof(AqaraCubeSensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_cube.aqgl01", 0, typeof(AqaraCubeSensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_cube", 0, typeof(AqaraCubeSensor)),
        (DeviceProtocol.XiaomiGateway2, "ctrl_neutral2", 0, typeof(AqaraDualWallSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.ctrl_neutral2", 0, typeof(AqaraDualWallSwitch)),
        (DeviceProtocol.XiaomiGateway2, "remote.b286acn01", 0, typeof(AqaraDualWallWirelessSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.remote.b286acn01", 0, typeof(AqaraDualWallWirelessSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_86sw2", 0, typeof(AqaraDualWallWirelessSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_86sw2.es1", 0, typeof(AqaraDualWallWirelessSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.relay.c2acn01", 0, typeof(AqaraDualWirelessRelayCN)),
        (DeviceProtocol.LumiMqtt, "lumi.remote.b486opcn01", 0, typeof(AqaraOppleFourButtonsWirelesSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.remote.b286opcn01", 0, typeof(AqaraOppleTwoButtonsWirelesSwitch)),
        (DeviceProtocol.XiaomiGateway2, "sensor_motion.aq2", 0, typeof(AqaraSmartHumanBodySensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_motion.aq2", 0, typeof(AqaraSmartHumanBodySensor)),
        (DeviceProtocol.LumiMqtt, "lumi.switch.n0agl1", 0, typeof(AqaraT1WithNeutral)),
        (DeviceProtocol.XiaomiGateway2, "weather.v1", 0, typeof(AqaraTemperatureHumiditySensor)),
        (DeviceProtocol.LumiMqtt, "lumi.weather", 0, typeof(AqaraTemperatureHumiditySensor)),
        (DeviceProtocol.XiaomiGateway2, "vibration", 0, typeof(AqaraVibrationSensor)),
        (DeviceProtocol.LumiMqtt, "lumi.vibration.aq1", 0, typeof(AqaraVibrationSensor)),
        (DeviceProtocol.XiaomiGateway2, "sensor_wleak.aq1", 0, typeof(AqaraWaterLeakSensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_wleak.aq1", 0, typeof(AqaraWaterLeakSensor)),
        (DeviceProtocol.XiaomiGateway2, "sensor_magnet.aq2", 0, typeof(AqaraWindowDoorSensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_magnet.aq2", 0, typeof(AqaraWindowDoorSensor)),
        (DeviceProtocol.XiaomiGateway2, "remote.b1acn01", 0, typeof(AqaraWirelessMiniSwitchCN)),
        (DeviceProtocol.LumiMqtt, "lumi.remote.b1acn01", 0, typeof(AqaraWirelessMiniSwitchCN)),
        (DeviceProtocol.LumiMqtt, "lumi.remote.acn007", 0, typeof(AqaraWirelessMiniSwitchCN2)),
        (DeviceProtocol.LumiBle, "090615.remote.btsw1", 14523, typeof(PtxWirelessSwitchBle)),
        (DeviceProtocol.LumiBle, "miaomiaoce.sensor_ht", 1371, typeof(XiaomiBluetoothHygrothermograph2)),
        (DeviceProtocol.LumiBle, "miaomiaoce.sensor_ht.t9", 10290, typeof(XiaomiBluetoothHygrothermograph3)),
        (DeviceProtocol.LumiBle, "isa.magnet.dw2hl", 2443, typeof(XiaomiDoorWindowSensor2)),
        (DeviceProtocol.XiaomiGateway2, "smoke", 0, typeof(XiaomiHoneywellSmokeDetector)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_smoke", 0, typeof(XiaomiHoneywellSmokeDetector)),
        (DeviceProtocol.LumiMesh, "zimi.switch.dhkg01", 1945, typeof(XiaomiMeshWallSwitchSingleOpen)),
        (DeviceProtocol.XiaomiGateway2, "plug", 0, typeof(XiaomiMiSmartPowerPlugCN)),
        (DeviceProtocol.LumiMqtt, "lumi.plug", 0, typeof(XiaomiMiSmartPowerPlugCN)),
        (DeviceProtocol.XiaomiGateway2, "motion", 0, typeof(XiaomiSmartHumanBodySensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_motion", 0, typeof(XiaomiSmartHumanBodySensor)),
        (DeviceProtocol.LumiBle, "lumi.motion.bmgl01", 2701, typeof(XiaomiSmartHumanBodySensor2)),
        (DeviceProtocol.XiaomiGateway2, "switch", 0, typeof(XiaomiSmartWirelessSwitch)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_switch", 0, typeof(XiaomiSmartWirelessSwitch)),
        (DeviceProtocol.LumiBle, "lumi.sensor_smoke.mcn02", 2455, typeof(XiaomiSmokeSensorGuard)),
        (DeviceProtocol.XiaomiGateway2, "sensor_ht", 0, typeof(XiaomiTemperatureHumiditySensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_ht", 0, typeof(XiaomiTemperatureHumiditySensor)),
        (DeviceProtocol.XiaomiGateway2, "magnet", 0, typeof(XiaomiWindowDoorSensor)),
        (DeviceProtocol.LumiMqtt, "lumi.sensor_magnet", 0, typeof(XiaomiWindowDoorSensor)),
    ];

    public static IEnumerable<object[]> ProtocolCases => Enum.GetValues<DeviceProtocol>()
        .Select(protocol => new object[] { protocol.ToString() });

    public static IEnumerable<object[]> RegistrationCases => Expected
        .Select(row => new object[] { row.Protocol.ToString(), row.Model, row.Pdid, row.Type });

    [Theory]
    [MemberData(nameof(ProtocolCases))]
    public void Registry_Contains_Exactly_The_Expected_Models_For_Protocol(string protocolName)
    {
        var protocol = Enum.Parse<DeviceProtocol>(protocolName);
        ProtocolDeviceRegistry.Find(protocol).Should().BeEquivalentTo(
            Expected.Where(row => row.Protocol == protocol).Select(row => row.Model));
    }

    [Theory]
    [MemberData(nameof(RegistrationCases))]
    public void Expected_Model_And_Pdid_Create_The_Expected_Device_Type(
        string protocolName, string model, int pdid, Type expectedType)
    {
        var protocol = Enum.Parse<DeviceProtocol>(protocolName);
        GatewaySubDeviceBase device;
        var supported = protocol switch
        {
            DeviceProtocol.LumiBle => ProtocolDeviceRegistry.TryCreateBle(pdid, "contract", NullLoggerFactory.Instance, out device),
            DeviceProtocol.LumiMesh => ProtocolDeviceRegistry.TryCreateMesh(pdid, "contract", NullLoggerFactory.Instance, out device),
            _ => ProtocolDeviceRegistry.TryCreate(protocol, model, "contract", 0, NullLoggerFactory.Instance, out device)
        };
        using (device)
        {
            supported.Should().BeTrue($"{protocol} {model} must remain supported");
            device.Should().BeOfType(expectedType);
            ((ProtocolMappedDeviceBase)device).Model.Should().Be(model);
        }
    }
}
