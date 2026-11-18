using MiHomeLib.Devices;
using MiHomeLib.MqttGateway.Discovery;
using MiHomeLib.Helpers;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;
using AutoFixture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MiHomeLib;
using MiHomeLib.MqttGateway;
using AqaraT1WithNeutral = MiHomeLib.Devices.AqaraT1WithNeutral;
using XiaomiBluetoothHygrothermograph3 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph3;
using XiaomiMeshWallSwitchSingleOpen = MiHomeLib.Devices.XiaomiMeshWallSwitchSingleOpen;
using UnifiedTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;
using UnifiedAqaraTemperatureHumiditySensor = MiHomeLib.Devices.AqaraTemperatureHumiditySensor;
using XiaomiSmartWirelessSwitch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;
using XiaomiBluetoothHygrothermograph2 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph2;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse.BleAsyncEventParams;
using static MiHomeUnitTests.Infrastructure.Messages.ZigbeeHearbeatResponse;
using static MiHomeUnitTests.Infrastructure.Messages.ZigbeeHearbeatResponse.ZigbeeHearbeatItem;

namespace MiHomeUnitTests.Gateways.Mqtt;

public class MqttGatewayMessageProcessingTests : MqttGatewayTestBase
{
    [Theory]
    [InlineData("")]
    [InlineData("AABBCCDDEEF")]
    [InlineData("AABBCCDDEEFF00")]
    [InlineData("AABBCCDDEEGG")]
    [InlineData("aa:bb:cc:dd:ee:ff")]
    public void Invalid_Encoded_Mac_Is_Rejected(string mac)
    {
        Action decode = () => Gateway.DecodeMacAddress(mac);
        decode.Should().Throw<FormatException>();
    }

    [Fact]
    public void Null_Encoded_Mac_Identifies_The_Parameter()
    {
        Action decode = () => Gateway.DecodeMacAddress(null);
        decode.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("mac");
    }

    [Theory]
    [InlineData("ffeeddccbbaa", "aa:bb:cc:dd:ee:ff")]
    [InlineData("FFEEDDCCBBAA", "AA:BB:CC:DD:EE:FF")]
    [InlineData("000000000000", "00:00:00:00:00:00")]
    public void Valid_Encoded_Mac_Preserves_Byte_Order_And_Case(string mac, string expected) =>
        Gateway.DecodeMacAddress(mac).Should().Be(expected);

    [Fact]
    public async Task MiniSwitchCN2_Receives_MiSpec_Events_And_Properties_Through_Gateway()
    {
        const string did = "lumi.mini";
        SetupZigBeeMiSpecDevices([(did, MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL)]);
        MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2 button = null;
        var clicks = new List<MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2.ClickArg>();
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            button = (MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2)device;
            button.OnClickAsync += click => { clicks.Add(click); return Task.CompletedTask; };
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        foreach (var eiid in new[] { 1, 2, 3 })
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                method = "event_occured", @params = new { did, siid = 2, eiid, arguments = new object[0] }
            });
            _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", payload);
        }
        var properties = System.Text.Json.JsonSerializer.Serialize(new
        {
            method = "properties_changed", @params = new[] { new { did, siid = 3, piid = 2, value = 3118 } }
        });
        // The gateway's central/report route is used for these MI-spec notifications.
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", properties);
        button.Should().NotBeNull();
        button.DeviceId.Should().Be(did);
        button.Voltage.Should().Be(3.118f);
        clicks.Should().Equal(MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2.ClickArg.SingleClick,
            MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2.ClickArg.DoubleClick,
            MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2.ClickArg.LongPressClick);
    }

    [Fact]
    public void ZigbeeReportCommand_Works()
    {
        // Arrange
        var eventRaised = false;
        var did = _fixture.Create<string>();
        var time = (double)1_700_000_000_000L;

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
        ]);

        DateTime? receivedAt = null;

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.DeviceId.Should().Be(did);
            var sw = device as XiaomiSmartWirelessSwitch;

            sw.OnClickAsync += clickArgs =>
            {
                eventRaised = clickArgs == XiaomiSmartWirelessSwitch.ClickArg.SingleClick;
                receivedAt = sw.LastMessageSourceTimeUtc;
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();

        RaiseZigbeeReportEvent(did, time, [("13.1.85", 1)]);

        // Assert
        eventRaised.Should().BeTrue();
        receivedAt.Should().Be(time.UnixMilliSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public void ZigbeeReportCommandWithMiSpec_Works()
    {
        // Arrange
        var eventRaised = false;
        var did = _fixture.Create<string>();
        var time = (double)1_700_000_000_000L;

        SetupZigBeeMiSpecDevices([
            (did, AqaraT1WithNeutral.LUMI_MQTT_MODEL),
        ]);

        DateTime? receivedAt = null;

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.DeviceId.Should().Be(did);

            var aqaraRelay = device as AqaraT1WithNeutral;

            aqaraRelay.OnLoadPowerChangeAsync += x =>
            {
                eventRaised = true;
                receivedAt = aqaraRelay.LastMessageSourceTimeUtc;
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();

        RaiseZigbeeMiSpecReportEvent(did, time, [(3, 2, 42.2)]);

        // Assert
        eventRaised.Should().BeTrue();
        receivedAt.Should().Be(time.UnixMilliSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public void ZigbeeHeartbeatCommand_Works()
    {
        // Arrange
        var voltageEventRaised = false;
        var batteryEventRaised = false;
        var oldVoltage = 3032;
        var newVoltage = 3012;
        var oldBatteryPerent = 100;
        var newBatteryPerent = 99;
        var did = _fixture.Create<string>();
        var time = (double)1_700_000_000_000L;

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [oldVoltage, oldBatteryPerent, 192, 88]),
        ]);

        float? reportedVoltage = null;
        byte? reportedBattery = null;
        XiaomiSmartWirelessSwitch sw = null;

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.DeviceId.Should().Be(did);

            sw = device as XiaomiSmartWirelessSwitch;

            sw.OnVoltageChangeAsync += value =>
            {
                voltageEventRaised = true;
                reportedVoltage = value;
                return Task.CompletedTask;
            };

            sw.OnBatteryPercentChangeAsync += value =>
            {
                batteryEventRaised = true;
                reportedBattery = value;
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();

        RaiseZigbeeHeartbeatEvent(did, time, [("8.0.2008", newVoltage), ("8.0.2001", newBatteryPerent)]);

        // Assert
        voltageEventRaised.Should().BeTrue();
        batteryEventRaised.Should().BeTrue();
        reportedVoltage.Should().Be(oldVoltage / 1000f, "change events pass the previous value");
        reportedBattery.Should().Be((byte)oldBatteryPerent);
        sw.Voltage.Should().Be(newVoltage / 1000f);
        sw.BatteryPercent.Should().Be((byte)newBatteryPerent);
        sw.LastMessageSourceTimeUtc.Should().Be(time.UnixMilliSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public void AsyncBleEventMethod_Works()
    {
        // Arrange
        var temperatureEventRaised = false;
        var did = _fixture.Create<string>();
        var mac = "ffeeddccbbaa";

        double time = 1_700_000_000L;

        SetupBleDevices([
            (did, XiaomiBluetoothHygrothermograph2.PDID, mac),
        ]);

        DateTime? receivedAt = null;

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.DeviceId.Should().Be(did);

            var th = device as XiaomiBluetoothHygrothermograph2;

            th.OnTemperatureChangeAsync += oldValue =>
            {
                temperatureEventRaised = true;
                receivedAt = th.LastMessageSourceTimeUtc;
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();

        RaiseBleAsyncEvent(XiaomiBluetoothHygrothermograph2.PDID, did, mac, time, [(4100, "e500")]);

        // Assert
        temperatureEventRaised.Should().BeTrue();
        receivedAt.Should().Be(time.UnixSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public void Message_From_Unsupported_Topic_Is_Not_Dispatched_To_Previous_Processor()
    {
        // Arrange
        var clicks = 0;
        var did = _fixture.Create<string>();
        var time = (double)1_700_000_000_000L;

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
        ]);

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            ((XiaomiSmartWirelessSwitch)device).OnClickAsync += _ =>
            {
                clicks++;
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        };

        Gateway.DiscoverDevices();

        RaiseZigbeeReportEvent(did, time, [("13.1.85", 1)]);

        // Act: same report payload, but from a topic the gateway doesn't handle
        var report = $"{{\"cmd\":\"report\",\"did\":\"{did}\",\"time\":{time},\"params\":[{{\"res_name\":\"13.1.85\",\"value\":1}}]}}";
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "some/other/topic", report);

        // Assert
        clicks.Should().Be(1);
    }
}
