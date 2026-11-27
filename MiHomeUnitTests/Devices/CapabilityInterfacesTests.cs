using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeUnitTests.Devices.Mqtt;
using Xunit;

namespace MiHomeUnitTests.Devices;

public class CapabilityInterfacesTests : MqttDeviceTestBase
{
    [Fact]
    public void Protocol_Device_Catalog_Implements_Only_The_Declared_Capabilities()
    {
        var entries = ProtocolDeviceCatalog.Entries.Select(entry => entry.DeviceType).ToArray();
        var lumiBatteryDevices = entries.Where(type => typeof(LumiBatteryDeviceBase).IsAssignableFrom(type)).ToArray();
        var batteryDevices = lumiBatteryDevices
            .Concat(
            [
                typeof(AqaraCubeSensor),
                typeof(AqaraDualWallWirelessSwitch),
                typeof(XiaomiBluetoothHygrothermograph2),
                typeof(XiaomiBluetoothHygrothermograph3),
                typeof(XiaomiDoorWindowSensor2),
                typeof(XiaomiSmartHumanBodySensor2),
                typeof(XiaomiSmokeSensorGuard),
            ]).ToArray();
        var linkQualityDevices = lumiBatteryDevices.Concat(
        [
            typeof(AqaraDualWirelessRelayCN),
            typeof(XiaomiHoneywellSmokeDetector),
            typeof(XiaomiMiSmartPowerPlugCN),
        ]).ToArray();
        var expected = new Dictionary<Type, Type[]>
        {
            [typeof(ITemperatureSensor)] =
            [
                typeof(XiaomiTemperatureHumiditySensor),
                typeof(AqaraTemperatureHumiditySensor),
                typeof(XiaomiBluetoothHygrothermograph2),
                typeof(XiaomiBluetoothHygrothermograph3),
            ],
            [typeof(IHumiditySensor)] =
            [
                typeof(XiaomiTemperatureHumiditySensor),
                typeof(AqaraTemperatureHumiditySensor),
                typeof(XiaomiBluetoothHygrothermograph2),
                typeof(XiaomiBluetoothHygrothermograph3),
            ],
            [typeof(IPressureSensor)] = [typeof(AqaraTemperatureHumiditySensor)],
            [typeof(IBatteryPowered)] = batteryDevices,
            [typeof(ILinkQualityReporter)] = linkQualityDevices,
            [typeof(IPowerMeter)] =
            [
                typeof(AqaraDualWirelessRelayCN),
                typeof(AqaraT1WithNeutral),
                typeof(XiaomiMiSmartPowerPlugCN),
            ],
            [typeof(IContactSensor)] =
            [
                typeof(XiaomiWindowDoorSensor),
                typeof(AqaraWindowDoorSensor),
                typeof(XiaomiDoorWindowSensor2),
            ],
            [typeof(ISmokeSensor)] =
            [
                typeof(XiaomiHoneywellSmokeDetector),
                typeof(XiaomiSmokeSensorGuard),
            ],
            [typeof(ISwitchable)] =
            [
                typeof(XiaomiMiSmartPowerPlugCN),
                typeof(AqaraT1WithNeutral),
                typeof(XiaomiMeshWallSwitchSingleOpen),
            ],
        };

        foreach (var (capability, expectedTypes) in expected)
        {
            var actualTypes = entries.Where(capability.IsAssignableFrom).ToArray();
            actualTypes.Should().BeEquivalentTo(expectedTypes, $"the {capability.Name} device list must match the plan");
        }
    }

    [Fact]
    public void Contact_And_Smoke_Capabilities_Expose_Unknown_And_Reported_States()
    {
        var contact = CreateLumiBleProtocolDevice<XiaomiDoorWindowSensor2>(XiaomiDoorWindowSensor2.PDID);
        var contactCapability = (IContactSensor)contact;
        contactCapability.IsOpen.Should().BeNull();
        contact.ParseData(SetupBleAsyncEventParams(4121, "00").ToString());
        contactCapability.IsOpen.Should().BeTrue();
        contact.ParseData(SetupBleAsyncEventParams(4121, "01").ToString());
        contactCapability.IsOpen.Should().BeFalse();

        var smoke = CreateLumiBleProtocolDevice<XiaomiSmokeSensorGuard>(XiaomiSmokeSensorGuard.PDID);
        var smokeCapability = (ISmokeSensor)smoke;
        smokeCapability.IsSmokeDetected.Should().BeNull();
        smoke.ParseData(SetupBleAsyncEventParams(4117, "01").ToString());
        smokeCapability.IsSmokeDetected.Should().BeTrue();
        smoke.ParseData(SetupBleAsyncEventParams(4117, "00").ToString());
        smokeCapability.IsSmokeDetected.Should().BeFalse();
    }

    [Fact]
    public async Task Switchable_Plug_Uses_The_Existing_Power_Command()
    {
        var plug = CreateLumiProtocolDevice<XiaomiMiSmartPowerPlugCN>(XiaomiMiSmartPowerPlugCN.LUMI_MQTT_MODEL);
        ((IProtocolMappedManageableDevice)plug).SetMqttTransport(_mqttTransport.Object);

        await ((ISwitchable)plug).TurnOnAsync();
        await plug.PowerOnAsync();

        _mqttTransport.Verify(x => x.SendMessage(
            $"{{\"cmd\":\"write\",\"did\":\"{plug.DeviceId}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}"),
            Moq.Times.Exactly(2));
    }
}
