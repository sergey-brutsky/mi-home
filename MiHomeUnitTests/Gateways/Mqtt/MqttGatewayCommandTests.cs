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

public class MqttGatewayCommandTests : MqttGatewayTestBase
{
    [Fact]
    public void PropertiesChanged_Reaches_Miot_Ble_Device()
    {
        // Arrange: MJWSD05MMC is discovered as a BLE device and reports in miot format
        var did = _fixture.Create<string>();

        SetupBleDevices([
            (did, XiaomiBluetoothHygrothermograph3.PDID, "79e0fe38c1a4"),
        ]);

        XiaomiBluetoothHygrothermograph3 th = null;
        var temperatureEventRaised = false;

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            th = device as XiaomiBluetoothHygrothermograph3;
            th.OnTemperatureChangeAsync += _ =>
            {
                temperatureEventRaised = true;
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        };

        // Act: same envelope as central/report properties_changed in the gateway logs
        Gateway.DiscoverDevices();

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report",
            $"{{\"id\":1,\"_to\":128,\"type\":16,\"method\":\"properties_changed\",\"params\":[{{\"did\":\"{did}\",\"siid\":3,\"piid\":1001,\"value\":21.5}}]}}");

        // Assert
        th.Should().NotBeNull();
        temperatureEventRaised.Should().BeTrue();
        th.Temperature.Should().Be(21.5f);
    }

    [Fact]
    public void PropertiesChanged_Unknown_Did_Does_Not_Drop_Rest_Of_Batch()
    {
        // Arrange
        var did = _fixture.Create<string>();

        SetupBleDevices([
            (did, XiaomiBluetoothHygrothermograph3.PDID, "79e0fe38c1a4"),
        ]);

        XiaomiBluetoothHygrothermograph3 th = null;
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            th = device as XiaomiBluetoothHygrothermograph3;
            return Task.CompletedTask;
        };

        Gateway.DiscoverDevices();

        // Act: one message carries a device the library doesn't know first, then a known one
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report",
            "{\"id\":1,\"method\":\"properties_changed\",\"params\":[" +
            "{\"did\":\"unknown-did\",\"siid\":2,\"piid\":1,\"value\":true}," +
            $"{{\"did\":\"{did}\",\"siid\":3,\"piid\":1001,\"value\":21.5}}]}}");

        // Assert
        th.Should().NotBeNull();
        th.Temperature.Should().Be(21.5f);
    }

    [Fact]
    public void UnifiedTemperatureHumiditySensor_UsesLumiMqttAdapter()
    {
        var did = _fixture.Create<string>();
        SetupZigBeeDevices([
            (did, UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2363, 2902, 95]),
        ]);

        Gateway.DiscoverDevices();

        var sensor = Gateway.GetDeviceByDid<UnifiedTemperatureHumiditySensor>(did);
        sensor.Should().NotBeNull();
        sensor.Temperature.Should().Be(23.63f);
        sensor.Humidity.Should().Be(29.02f);
        sensor.BatteryPercent.Should().Be((byte)95);
        sensor.Voltage.Should().Be(0f);
        sensor.LinkQuality.Should().Be((byte)0);
        sensor.ChipTemperature.Should().Be((byte)0);
        sensor.ToString().Should().Contain("BatteryPercent: 95%");
        sensor.ToString().Should().NotContain("Voltage:")
            .And.NotContain("LinkQuality:")
            .And.NotContain("ChipTemperature:");
        _miioTransport.Verify(
            x => x.SendMessageAsync(
                It.Is<string>(request =>
                    request.Contains("\"temperature\"") &&
                    request.Contains("\"humidity\"") &&
                    request.Contains("\"battery\"") &&
                    !request.Contains("\"voltage\"") &&
                    !request.Contains("\"lqi\"") &&
                    !request.Contains("\"chip_temperature\""))),
            Times.Once,
            "WSDCGQ01LM on the Multimode gateway exposes only temperature, humidity and battery during initial property reads");

        float reportedTemperature = 0;
        sensor.OnTemperatureChangeAsync += value =>
        {
            reportedTemperature = value;
            return Task.CompletedTask;
        };

        RaiseZigbeeReportEvent(did, 1_700_000_000_000L, [("0.1.85", 2415)]);

        sensor.Temperature.Should().Be(24.15f);
        reportedTemperature.Should().Be(23.63f, "change events pass the previous value");
    }

    [Fact]
    public void ZigbeeReadResponse_Updates_Device_And_Skips_Errors()
    {
        var did = _fixture.Create<string>();
        SetupZigBeeDevices([
            (did, UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2363, 2902, 95]),
        ]);
        Gateway.DiscoverDevices();
        var sensor = Gateway.GetDeviceByDid<UnifiedTemperatureHumiditySensor>(did);
        float? oldTemperature = null;
        sensor.OnTemperatureChangeAsync += x => { oldTemperature = x; return Task.CompletedTask; };

        // Same shape as the gateway sends it: one result with a value, one the device does not support
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "zigbee/send",
            $"{{\"cmd\":\"read_rsp\",\"id\":340,\"did\":\"{did}\",\"time\":1794564562637,\"zseq\":74," +
            "\"results\":[{\"res_name\":\"0.1.85\",\"value\":2415,\"error_code\":0}," +
            "{\"res_name\":\"8.0.2031\",\"error_code\":-4003}]}");

        sensor.Temperature.Should().Be(24.15f);
        oldTemperature.Should().Be(23.63f);
        sensor.LastMessageSourceTimeUtc.Should().Be(1794564562637d.UnixMilliSecondsToDateTime().ToUniversalTime());
        _loggerFactory.Entries.Should().NotContain(x => x.Level >= LogLevel.Warning && x.Message.Contains("read_rsp"));
    }

    [Fact]
    public void ZigbeeReadResponse_About_The_Gateway_Itself_Is_Not_A_Warning()
    {
        SetupZigBeeDevices([]);
        Gateway.DiscoverDevices();

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "zigbee/send",
            "{\"cmd\":\"read_rsp\",\"id\":350,\"did\":\"lumi.0\",\"time\":1794564562637," +
            "\"results\":[{\"res_name\":\"8.0.2080\",\"value\":\"6.6.2-126.32_1.14\",\"error_code\":0}]}");

        _loggerFactory.Entries.Should().NotContain(x => x.Level >= LogLevel.Warning && (x.Message.Contains("read_rsp") || x.Message.Contains("lumi.0")));
    }

    [Fact]
    public void UnifiedAqaraTemperatureHumiditySensor_UsesLumiMqttAdapter()
    {
        var did = _fixture.Create<string>();
        SetupZigBeeDevices([
            (did, UnifiedAqaraTemperatureHumiditySensor.LUMI_MQTT_MODEL, [99120, 2363, 2902, 3025, 95, 180, 34]),
        ]);

        Gateway.DiscoverDevices();

        var sensor = Gateway.GetDeviceByDid<UnifiedAqaraTemperatureHumiditySensor>(did);
        sensor.Should().NotBeNull();
        sensor.Temperature.Should().Be(23.63f);
        sensor.Humidity.Should().Be(29.02f);
        sensor.Pressure.Should().Be(991.2f);

        float reportedPressure = 0;
        sensor.OnPressureChangeAsync += value =>
        {
            reportedPressure = value;
            return Task.CompletedTask;
        };

        RaiseZigbeeReportEvent(did, 1_700_000_000_000L, [("0.3.85", 99200)]);

        sensor.Pressure.Should().Be(992f);
        reportedPressure.Should().Be(991.2f, "change events pass the previous value");
    }

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(false, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(true, 0, false)]
    public async Task Get_AccidentalDeletionEnabled_Should_Return_Correct_Value(bool isAsync, int value, bool expected)
    {
        // Arrange
        SetupGetProperties((7, 1, _gwDid, value));

        // Act
        var accidentalDeletion = isAsync
            ? await Gateway.GetAccidentalDeletionEnabledAsync()
            : Gateway.GetAccidentalDeletionEnabled();

        // Assert
        VerifyGetProperties(7, 1, _gwDid);
        accidentalDeletion.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 1)]
    public async Task Set_Accidental_Deletion_Should_Work_As_Expected(bool isAsync, bool value, int expected)
    {
        // Arrange
        SetupSetProperties();

        // Act
        if (isAsync) await Gateway.SetAccidentalDeletionEnabledAsync(value);
        else Gateway.SetAccidentalDeletionEnabled(value);

        // Assert
        VerifySetProperties(7, 1, _gwDid, expected, 2);
    }

    [Fact]
    public void Gw_Properties_Returns_Valid_Values()
    {
        // Arrange
        var expectedUptime = _fixture.Create<uint>();
        var expectedMiioVersion = "0.0.9";
        var expectedMac = "34:EF:44:49:BC:63";
        var expectedFirmwareVersion = "1.0.5_0008";
        var expectedHardwareVersion = "Linux";
        var expectedSsid = _fixture.Create<string>();
        var expectedBssid = _fixture.Create<string>();
        var expectedRssi = _fixture.Create<int>();
        var expectedLocalIp = "192.168.1.100";
        var expectedMask = "255.255.255.0";
        var expectedGw = "192.168.1.1";

        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("miIO.info"))))
            .ReturnsAsync(new
            {
                id = 1,
                result = new
                {
                    uptime = expectedUptime,
                    life = expectedUptime,
                    miio_ver = expectedMiioVersion,
                    mac = expectedMac,
                    fw_ver = expectedFirmwareVersion,
                    hw_ver = expectedHardwareVersion,
                    ap = new
                    {
                        ssid = expectedSsid,
                        bssid = expectedBssid,
                        rssi = expectedRssi,
                    },
                    netif = new
                    {
                        localIp = expectedLocalIp,
                        mask = expectedMask,
                        gw = expectedGw,
                    }
                }
            }.ToJson());

        // Assert
        Gateway.UptimeSeconds.Should().Be(expectedUptime);
        Gateway.MiioVersion.Should().Be(expectedMiioVersion);
        Gateway.Mac.Should().Be(expectedMac);
        Gateway.FirmwareVersion.Should().Be(expectedFirmwareVersion);
        Gateway.Hardware.Should().Be(expectedHardwareVersion);
        Gateway.Wifi.Ssid.Should().Be(expectedSsid);
        Gateway.Wifi.Bssid.Should().Be(expectedBssid);
        Gateway.Wifi.Rssi.Should().Be(expectedRssi);
        Gateway.Network.Ip.Should().Be(expectedLocalIp);
        Gateway.Network.Mask.Should().Be(expectedMask);
        Gateway.Network.Gateway.Should().Be(expectedGw);
    }
}
