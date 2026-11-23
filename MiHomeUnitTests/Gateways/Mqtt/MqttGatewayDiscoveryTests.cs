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

public class MqttGatewayDiscoveryTests : MqttGatewayTestBase
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Discovery_Buffer_Overflow_Fails_And_Allows_Retry(bool byteLimit, bool synchronousSubscriber)
    {
        SetupZigBeeDevices([("buffer-switch", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Gateway.MaxInitializationMessages = byteLimit ? 256 : 1;
        Gateway.MaxInitializationBytes = byteLimit ? 1 : 8 * 1024 * 1024;
        System.Func<MiHomeLib.Devices.GatewaySubDeviceBase, Task> subscriber = async _ =>
        {
            entered.TrySetResult(true);
            if (synchronousSubscriber) release.Task.GetAwaiter().GetResult();
            else await release.Task;
        };
        Gateway.OnDeviceDiscoveredAsync += subscriber;
        var discovery = Gateway.DiscoverDevicesAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            RaiseZigbeeReportEvent("buffer-switch", 1794564562637, [("13.1.85", 1)]);
            RaiseZigbeeReportEvent("buffer-switch", 1794564562638, [("13.1.85", 2)]);
            System.Func<Task> finish = async () => await discovery.WaitAsync(TimeSpan.FromSeconds(2));
            await finish.Should().ThrowAsync<System.IO.IOException>().WithMessage("*buffer*full*");
        }
        finally { release.TrySetResult(true); Gateway.OnDeviceDiscoveredAsync -= subscriber; }
        await Gateway.DiscoverDevicesAsync();
        RaiseZigbeeReportEvent("buffer-switch", 1794564562639, [("8.0.2008", 3300)]);
        Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>("buffer-switch").Voltage.Should().Be(3.3f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Individual_Initial_Properties_Survive_Another_Property_Failure(bool timeout)
    {
        const string did = "partial-switch";
        SetupZigBeeDevices([(did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_device_prop"))))
            .Returns<string>(request =>
            {
                var parameters = System.Text.Json.Nodes.JsonNode.Parse(request)["params"].AsArray();
                if (parameters.Count != 2) return Task.FromResult("{\"code\":0,\"result\":[]}");
                return parameters[1].GetValue<string>() switch
                {
                    "voltage" => Task.FromResult("{\"code\":0,\"result\":[3200]}"),
                    "lqi" => Task.FromResult("{\"code\":0,\"result\":[123]}"),
                    "chip_temperature" => Task.FromResult("{\"code\":0,\"result\":[24]}"),
                    _ => timeout ? Task.FromException<string>(new TimeoutException("battery"))
                        : Task.FromResult("{\"code\":-1}")
                };
            });
        await Gateway.DiscoverDevicesAsync();
        var sensor = Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did);
        sensor.Voltage.Should().Be(3.2f);
        sensor.LinkQuality.Should().Be(123);
        sensor.ChipTemperature.Should().Be(24);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_During_Initial_Read_Are_Replayed_After_Client_Subscribes(bool timeout)
    {
        const string did = "initializing-switch";
        SetupZigBeeDevices([(did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_device_prop"))))
            .Returns(() => { entered.TrySetResult(true); return response.Task; });
        var clicks = new List<XiaomiSmartWirelessSwitch.ClickArg>();
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.LastTimeMessageReceived.Should().BeNull();
            ((XiaomiSmartWirelessSwitch)device).OnClickAsync += click =>
            {
                clicks.Add(click);
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        };
        var discovery = Gateway.DiscoverDevicesAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        RaiseZigbeeReportEvent(did, 1794564562636, [("8.0.2008", 3200)]);
        RaiseZigbeeReportEvent(did, 1794564562637, [("13.1.85", 1)]);
        RaiseZigbeeReportEvent(did, 1794564562638, [("13.1.85", 2)]);
        clicks.Should().BeEmpty();
        if (timeout) response.SetException(new TimeoutException("initial read"));
        else response.SetResult("{\"code\":0,\"result\":[3000,90,120,25]}");
        await discovery.WaitAsync(TimeSpan.FromSeconds(2));
        Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did).Voltage.Should().Be(3.2f);
        clicks.Should().Equal(XiaomiSmartWirelessSwitch.ClickArg.SingleClick, XiaomiSmartWirelessSwitch.ClickArg.DoubleClick);
        RaiseZigbeeReportEvent(did, 1794564562639, [("13.1.85", 3)]);
        clicks.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_Await_Discovery_Subscriber_And_Are_Discarded_On_Disposal(bool dispose)
    {
        const string did = "disposing-switch";
        SetupZigBeeDevices([(did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clicks = 0;
        Gateway.OnDeviceDiscoveredAsync += async device =>
        {
            ((XiaomiSmartWirelessSwitch)device).OnClickAsync += _ => { clicks++; return Task.CompletedTask; };
            entered.SetResult(true);
            await release.Task;
        };
        var discovery = Gateway.DiscoverDevicesAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        RaiseZigbeeReportEvent(did, 1794564562637, [("13.1.85", 1)]);
        clicks.Should().Be(0);
        if (dispose) Gateway.Dispose();
        release.SetResult(true);
        Func<Task> finish = async () => await discovery.WaitAsync(TimeSpan.FromSeconds(2));
        if (dispose)
            await finish.Should().ThrowAsync<OperationCanceledException>();
        else
            await finish();
        clicks.Should().Be(dispose ? 0 : 1);
    }

    [Fact]
    public async Task Async_Discoverer_Receives_The_Gateway_Lifetime_Token()
    {
        var discoverer = new Mock<IDevicesDiscoverer>();
        var asyncDiscoverer = discoverer.As<IAsyncDevicesDiscoverer>();
        asyncDiscoverer.Setup(value => value.DiscoverZigBeeDevicesAsync(It.Is<System.Threading.CancellationToken>(token => token.CanBeCanceled)))
            .ReturnsAsync(new List<ZigBeeDeviceInfo>());
        asyncDiscoverer.Setup(value => value.DiscoverBleDevicesAsync(It.Is<System.Threading.CancellationToken>(token => token.CanBeCanceled)))
            .ReturnsAsync(new List<BleDeviceInfo>());
        asyncDiscoverer.Setup(value => value.DiscoverMeshDevicesAsync(It.Is<System.Threading.CancellationToken>(token => token.CanBeCanceled)))
            .ReturnsAsync(new List<MeshDeviceInfo>());
        using var gateway = new Mock<MqttGatewayBase>(_gwDid, _miioTransport.Object, _mqttTransport.Object,
            discoverer.Object, _loggerFactory) { CallBase = true }.Object;

        await gateway.DiscoverDevicesAsync();

        asyncDiscoverer.VerifyAll();
        discoverer.Verify(value => value.DiscoverZigBeeDevices(), Times.Never);
        discoverer.Verify(value => value.DiscoverBleDevices(), Times.Never);
        discoverer.Verify(value => value.DiscoverMeshDevices(), Times.Never);
    }

    [Fact]
    public async Task Discovery_Can_Be_Retried_After_Invalid_Mac_Is_Corrected()
    {
        SetupBleDevices([("invalid-mac-device", XiaomiBluetoothHygrothermograph2.PDID, "invalid")]);
        Func<Task> discover = () => Gateway.DiscoverDevicesAsync();
        await discover.Should().ThrowAsync<FormatException>().WithMessage("*MAC*");
        Gateway.GetDevices().Should().BeEmpty();

        SetupBleDevices([("invalid-mac-device", XiaomiBluetoothHygrothermograph2.PDID, "ffeeddccbbaa")]);
        await Gateway.DiscoverDevicesAsync();
        Gateway.GetDeviceByDid<XiaomiBluetoothHygrothermograph2>("invalid-mac-device").Mac.Should().Be("aa:bb:cc:dd:ee:ff");
    }

    [Fact]
    public async Task Failing_Discovery_Subscriber_Does_Not_Stop_Other_Subscribers_Or_Devices()
    {
        SetupZigBeeDevices([
            ("first-event", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25]),
            ("second-event", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        var found = new List<string>();
        Gateway.OnDeviceDiscoveredAsync += _ => Task.FromException(new Exception("subscriber"));
        Gateway.OnDeviceDiscoveredAsync += device => { found.Add(device.DeviceId); return Task.CompletedTask; };
        await Gateway.DiscoverDevicesAsync();
        found.Should().Equal("first-event", "second-event");
        _loggerFactory.TakeErrors().Should().HaveCount(2);
    }

    [Fact]
    public async Task Dispose_Cancels_Discovery_Awaiting_Client_Handler()
    {
        SetupZigBeeDevices([("cancel-test", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Gateway.OnDeviceDiscoveredAsync += async _ => { entered.SetResult(true); await release.Task; };
        var discovery = Gateway.DiscoverDevicesAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Gateway.Dispose();
        Func<Task> wait = async () => await discovery.WaitAsync(TimeSpan.FromSeconds(1));
        await wait.Should().ThrowAsync<OperationCanceledException>();
        release.SetResult(true);
        Func<Task> repeat = () => Gateway.DiscoverDevicesAsync();
        await repeat.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(MiHomeLib.Devices.AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    [InlineData(MiHomeLib.Devices.AqaraOppleFourButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    public async Task Opple_Discovery_And_Mqtt_Reports_Reach_The_Device(string model)
    {
        const string did = "lumi.opple";
        SetupZigBeeDevices([(did, model, new[] { 3000, 90, 120, 25 })]);
        MiHomeLib.Devices.AqaraOppleWirelesSwitch device = null;
        var clicks = new List<MiHomeLib.Devices.AqaraOppleWirelesSwitch.ClickArg>();
        Gateway.OnDeviceDiscoveredAsync += found =>
        {
            device = (MiHomeLib.Devices.AqaraOppleWirelesSwitch)found;
            if (device is MiHomeLib.Devices.AqaraOppleTwoButtonsWirelessSwitch two)
                two.OnButton2ClickAsync += click => { clicks.Add(click); return Task.CompletedTask; };
            else
                ((MiHomeLib.Devices.AqaraOppleFourButtonsWirelessSwitch)device).OnButton4ClickAsync += click => { clicks.Add(click); return Task.CompletedTask; };
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        device.Should().NotBeNull();
        device.DeviceId.Should().Be(did);
        device.Voltage.Should().Be(3);
        device.BatteryPercent.Should().Be(90);
        device.LinkQuality.Should().Be(120);
        device.ChipTemperature.Should().Be(25);
        var resource = device is MiHomeLib.Devices.AqaraOppleTwoButtonsWirelessSwitch ? "13.2.85" : "13.4.85";
        RaiseZigbeeReportEvent(did, 1700000000000, [(resource, 3)]);
        RaiseZigbeeHeartbeatEvent(did, 1700000001000, [("8.0.2001", 75)]);
        clicks.Should().Equal(MiHomeLib.Devices.AqaraOppleWirelesSwitch.ClickArg.TripleClick);
        device.BatteryPercent.Should().Be(75);
    }

    [Fact]
    public async Task Ptx_Ble_Discovery_And_MiSpec_Events_Reach_The_Button()
    {
        const string did = "blt.ptx";
        SetupBleDevices([(did, MiHomeLib.Devices.PtxWirelessSwitchBle.PDID, "ffeeddccbbaa")]);
        MiHomeLib.Devices.PtxWirelessSwitchBle button = null;
        var clicks = new List<MiHomeLib.Devices.PtxWirelessSwitchBle.ClickArg>();
        var lowBattery = 0;
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            button = (MiHomeLib.Devices.PtxWirelessSwitchBle)device;
            button.OnClickAsync += click => { clicks.Add(click); return Task.CompletedTask; };
            button.OnLowBatteryAsync += () => { lowBattery++; return Task.CompletedTask; };
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        button.Should().NotBeNull();
        button.DeviceId.Should().Be(did);
        button.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        foreach (var eiid in new[] { 1012, 1013, 1014, 9999 })
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                method = "event_occured", @params = new { did, siid = 2, eiid, arguments = new object[0] }
            });
            _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", payload);
        }
        var low = System.Text.Json.JsonSerializer.Serialize(new
        {
            method = "event_occured", @params = new { did, siid = 3, eiid = 1 }
        });
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", low);
        clicks.Should().Equal(MiHomeLib.Devices.PtxWirelessSwitchBle.ClickArg.SingleClick,
            MiHomeLib.Devices.PtxWirelessSwitchBle.ClickArg.DoubleClick,
            MiHomeLib.Devices.PtxWirelessSwitchBle.ClickArg.LongPressClick);
        lowBattery.Should().Be(1);
    }

    [Fact]
    public async Task DoorWindowSensor2_Discovery_And_Ble_Report_Update_All_Properties()
    {
        const string did = "blt.contact";
        const double time = 1700000000;
        SetupBleDevices([(did, MiHomeLib.Devices.XiaomiDoorWindowSensor2.PDID, "ffeeddccbbaa")]);
        MiHomeLib.Devices.XiaomiDoorWindowSensor2 sensor = null;
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            sensor = (MiHomeLib.Devices.XiaomiDoorWindowSensor2)device;
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        sensor.Should().NotBeNull();
        sensor.DeviceId.Should().Be(did);
        sensor.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        RaiseBleAsyncEvent(MiHomeLib.Devices.XiaomiDoorWindowSensor2.PDID, did, sensor.Mac, time,
            [(4121, "02"), (4120, "01"), (4106, "64")]);
        sensor.Contact.Should().Be(MiHomeLib.Devices.XiaomiDoorWindowSensor2.ContactState.NotClosedAfterConfiguredTimeout);
        sensor.Light.Should().Be(MiHomeLib.Devices.XiaomiDoorWindowSensor2.LightState.LightDiscovered);
        sensor.BatteryPercent.Should().Be(100);
        sensor.LastMessageSourceTimeUtc.Should().Be(time.UnixSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task SmokeGuard_Discovery_And_Ble_Reports_Reach_The_Device()
    {
        const string did = "blt.smoke";
        const double time = 1700000000;
        SetupBleDevices([(did, MiHomeLib.Devices.XiaomiSmokeSensorGuard.PDID, "ffeeddccbbaa")]);
        MiHomeLib.Devices.XiaomiSmokeSensorGuard sensor = null;
        var smokeEvents = 0;
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            sensor = (MiHomeLib.Devices.XiaomiSmokeSensorGuard)device;
            sensor.OnSmokeChangeAsync += _ => { smokeEvents++; return Task.CompletedTask; };
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        sensor.Should().NotBeNull();
        sensor.DeviceId.Should().Be(did);
        sensor.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        sensor.ToString().Should().Contain(sensor.Mac).And.Contain("Smoke state: Unknown")
            .And.Contain("BatteryPercent: Unknown");
        RaiseBleAsyncEvent(MiHomeLib.Devices.XiaomiSmokeSensorGuard.PDID, did, sensor.Mac, time,
            [(4117, "01"), (4106, "64")]);
        sensor.Smoke.Should().Be(MiHomeLib.Devices.XiaomiSmokeSensorGuard.SmokeState.SmokeDetected);
        sensor.BatteryPercent.Should().Be(100);
        smokeEvents.Should().Be(1);
        sensor.LastMessageSourceTimeUtc.Should().Be(time.UnixSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task MotionSensor2_Discovery_And_Ble_Reports_Reach_The_Device()
    {
        const string did = "blt.motion";
        const double time = 1700000000;
        SetupBleDevices([(did, MiHomeLib.Devices.XiaomiSmartHumanBodySensor2.PDID, "ffeeddccbbaa")]);
        MiHomeLib.Devices.XiaomiSmartHumanBodySensor2 sensor = null;
        var idle = new List<MiHomeLib.Devices.XiaomiSmartHumanBodySensor2.NoMotionState>();
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            sensor = (MiHomeLib.Devices.XiaomiSmartHumanBodySensor2)device;
            sensor.OnNoMotionDetectedAsync += duration => { idle.Add(duration); return Task.CompletedTask; };
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        sensor.Should().NotBeNull();
        sensor.DeviceId.Should().Be(did);
        sensor.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        sensor.ToString().Should().Contain(sensor.Mac).And.Contain("Motion: Unknown")
            .And.Contain("Light: Unknown").And.Contain("BatteryPercent: Unknown");
        RaiseBleAsyncEvent(MiHomeLib.Devices.XiaomiSmartHumanBodySensor2.PDID, did, sensor.Mac, time,
            [(15, "000100"), (4120, "01"), (4119, "78000000"), (4106, "64")]);
        sensor.Motion.Should().Be(MiHomeLib.Devices.XiaomiSmartHumanBodySensor2.MotionState.MotionWithLight);
        sensor.Light.Should().Be(MiHomeLib.Devices.XiaomiSmartHumanBodySensor2.LightState.LightOn);
        sensor.BatteryPercent.Should().Be(100);
        idle.Should().Equal(MiHomeLib.Devices.XiaomiSmartHumanBodySensor2.NoMotionState.Idle120Seconds);
        sensor.LastMessageSourceTimeUtc.Should().Be(time.UnixSecondsToDateTime().ToUniversalTime());
    }

    [Fact]
    public void OnDeviceDiscovered_Works()
    {
        // Arrange
        var eventRaised = false;
        var switchDid = _fixture.Create<string>();
        var thDid = _fixture.Create<string>();
        var mac = "ffeeddccbbaa";

        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
        ]);

        SetupBleDevices([
            (thDid, XiaomiBluetoothHygrothermograph2.PDID, mac),
        ]);

        var counter = 0;

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            counter++;
            eventRaised = true;

            if (device is XiaomiSmartWirelessSwitch sw) sw.DeviceId.Should().Be(switchDid);
            if (device is XiaomiBluetoothHygrothermograph2 th) th.DeviceId.Should().Be(thDid);

            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();

        // Assert
        eventRaised.Should().BeTrue();
        counter.Should().Be(2);
    }

    [Fact]
    public void Repeated_Discovery_Keeps_Devices_And_Does_Not_Double_Messages()
    {
        // Arrange
        var clicks = 0;
        var discovered = 0;
        var did = _fixture.Create<string>();
        var time = (double)1_700_000_000_000L;

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
        ]);

        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered++;
            ((XiaomiSmartWirelessSwitch)device).OnClickAsync += _ =>
            {
                clicks++;
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();
        var first = Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did);
        Gateway.Invoking(x => x.DiscoverDevices()).Should().NotThrow();

        RaiseZigbeeReportEvent(did, time, [("13.1.85", 1)]);

        // Assert: same device instance, reported once, one message handled once
        Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did).Should().BeSameAs(first);
        Gateway.GetDevices().Should().ContainSingle();
        discovered.Should().Be(1);
        clicks.Should().Be(1);
    }

    [Theory]
    [InlineData(AqaraT1WithNeutral.LUMI_MQTT_MODEL)]
    [InlineData(MiHomeLib.Devices.AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL)]
    public async Task Discovery_Does_Not_Request_Empty_Initial_Properties(string model)
    {
        _devicesDiscoverer.Setup(x => x.DiscoverZigBeeDevices())
            .Returns([new ZigBeeDeviceInfo("lumi.mispec", model)]);

        await Gateway.DiscoverDevicesAsync();

        Gateway.GetDevices().Should().ContainSingle(x => x.DeviceId == "lumi.mispec");
        _miioTransport.Verify(x => x.SendMessageAsync(
            It.Is<string>(request => request.Contains("get_device_prop"))), Times.Never);
    }

    [Fact]
    public async Task Discovery_Skips_Unsupported_Devices_And_Does_Not_Mix_Ble_And_Mesh_Registrations()
    {
        SetupZigBeeDevices([
            ("unknown-zigbee", "lumi.unsupported", []),
            ("known-zigbee", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
        ]);
        SetupBleDevices([
            ("unknown-ble", -1, "79e0fe38c1a4"),
            ("mesh-in-ble", XiaomiMeshWallSwitchSingleOpen.PDID, "79e0fe38c1a4"),
            ("known-ble", XiaomiBluetoothHygrothermograph2.PDID, "79e0fe38c1a4"),
        ]);
        _devicesDiscoverer.Setup(x => x.DiscoverMeshDevices()).Returns([
            new MeshDeviceInfo("unknown-mesh", -1, "AA:BB:CC:DD:EE:FF"),
            new MeshDeviceInfo("ble-in-mesh", XiaomiBluetoothHygrothermograph2.PDID, "AA:BB:CC:DD:EE:FF"),
            new MeshDeviceInfo("known-mesh", XiaomiMeshWallSwitchSingleOpen.PDID, "AA:BB:CC:DD:EE:FF"),
        ]);
        var discovered = new List<string>();
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered.Add(device.DeviceId);
            return Task.CompletedTask;
        };

        await Gateway.DiscoverDevicesAsync();

        discovered.Should().Equal("known-zigbee", "known-ble", "known-mesh");
        Gateway.GetDevices().Select(x => x.DeviceId).Should().BeEquivalentTo(discovered);
        _loggerFactory.Entries.Count(x => x.Level == LogLevel.Warning && x.Message.Contains("not supported yet"))
            .Should().Be(5);
        _miioTransport.Verify(x => x.SendMessageAsync(
            It.Is<string>(request => request.Contains("unknown-zigbee"))), Times.Never);
    }

    [Fact]
    public async Task Repeated_Discovery_Preserves_Ble_And_Mesh_State_And_Subscriptions()
    {
        SetupBleDevices([("ble-did", XiaomiBluetoothHygrothermograph2.PDID, "79e0fe38c1a4")]);
        _devicesDiscoverer.Setup(x => x.DiscoverMeshDevices())
            .Returns([new MeshDeviceInfo("mesh-did", XiaomiMeshWallSwitchSingleOpen.PDID, "AA:BB:CC:DD:EE:FF")]);
        var discovered = new List<GatewaySubDeviceBase>();
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered.Add(device);
            return Task.CompletedTask;
        };
        await Gateway.DiscoverDevicesAsync();
        var ble = Gateway.GetDeviceByDid<XiaomiBluetoothHygrothermograph2>("ble-did");
        var mesh = Gateway.GetDeviceByDid<XiaomiMeshWallSwitchSingleOpen>("mesh-did");
        RaiseBleAsyncEvent(XiaomiBluetoothHygrothermograph2.PDID, "ble-did", ble.Mac, 123, [(4106, "5f")]);
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report",
            "{\"method\":\"properties_changed\",\"params\":[{\"did\":\"mesh-did\",\"siid\":2,\"piid\":1,\"value\":true}]}");

        await Gateway.DiscoverDevicesAsync();

        Gateway.GetDeviceByDid<XiaomiBluetoothHygrothermograph2>("ble-did").Should().BeSameAs(ble);
        Gateway.GetDeviceByDid<XiaomiMeshWallSwitchSingleOpen>("mesh-did").Should().BeSameAs(mesh);
        Gateway.GetDevices().Should().HaveCount(2);
        discovered.Should().HaveCount(2);
        ble.BatteryPercent.Should().Be(95);
        mesh.IsTurnedOn.Should().BeTrue();
        var batteryEvents = 0;
        ble.OnBatteryPercentChangeAsync += _ =>
        {
            batteryEvents++;
            return Task.CompletedTask;
        };
        RaiseBleAsyncEvent(XiaomiBluetoothHygrothermograph2.PDID, "ble-did", ble.Mac, 124, [(4106, "60")]);
        batteryEvents.Should().Be(1);
        ble.BatteryPercent.Should().Be(96);
    }

    [Fact]
    public void Failed_Initialization_Disposes_Both_Transports()
    {
        _miioTransport.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Throws(new TimeoutException());
        Action create = () => new MiHomeLib.MqttGateway.MultimodeGateway("failed", _miioTransport.Object,
            _mqttTransport.Object, _devicesDiscoverer.Object, _loggerFactory);
        create.Should().Throw<TimeoutException>();
        _mqttTransport.Verify(x => x.Dispose(), Times.Once);
        _miioTransport.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Concurrent_Discovery_Only_Adds_And_Notifies_Once()
    {
        SetupZigBeeDevices([("concurrent", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3000, 90, 120, 25])]);
        var notifications = 0;
        Gateway.OnDeviceDiscoveredAsync += async _ =>
        {
            System.Threading.Interlocked.Increment(ref notifications);
            await Task.Delay(30);
        };
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Gateway.DiscoverDevicesAsync())));
        Gateway.GetDevices().Should().ContainSingle();
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Discovers_Same_Devices_As_DiscoverDevices()
    {
        // Arrange
        var did = _fixture.Create<string>();

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
        ]);
        SetupBleDevices([
            ("ble-did", XiaomiBluetoothHygrothermograph2.PDID, "79e0fe38c1a4"),
        ]);
        _devicesDiscoverer
            .Setup(x => x.DiscoverMeshDevices())
            .Returns([new MeshDeviceInfo("1063744008", XiaomiMeshWallSwitchSingleOpen.PDID, "AA:BB:CC:DD:EE:FF")]);

        // Act
        await Gateway.DiscoverDevicesAsync();

        // Assert
        Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did).Should().NotBeNull();
        Gateway.GetDeviceByDid<XiaomiBluetoothHygrothermograph2>("ble-did").Mac.Should().Be("a4:c1:38:fe:e0:79");
        Gateway.GetDeviceByDid<XiaomiMeshWallSwitchSingleOpen>("1063744008").Should().NotBeNull();
    }

    [Fact]
    public void MeshDevice_Discovered_With_Mac_As_Stored_And_Commands_Go_To_MiioCommand()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var mac = "AA:BB:CC:DD:EE:FF";

        _devicesDiscoverer
            .Setup(x => x.DiscoverMeshDevices())
            .Returns([new MeshDeviceInfo(did, XiaomiMeshWallSwitchSingleOpen.PDID, mac)]);

        XiaomiMeshWallSwitchSingleOpen discovered = null;
        Gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered = device as XiaomiMeshWallSwitchSingleOpen;
            return Task.CompletedTask;
        };

        // Act
        Gateway.DiscoverDevices();
        discovered.Should().NotBeNull();
        discovered.TurnOn();
        var clicks = 0;
        discovered.OnClickAsync += () => { clicks++; return Task.CompletedTask; };
        var propertyPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            method = "properties_changed", @params = new[] { new { did, siid = 2, piid = 1, value = true } }
        });
        var eventPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            method = "event_occured", @params = new { did, siid = 8, eiid = 1, arguments = new object[0] }
        });
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", propertyPayload);
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", eventPayload);
        discovered.IsTurnedOn.Should().BeTrue();
        clicks.Should().Be(1);

        // Assert: mesh MAC is kept as stored, not byte-reversed like gateway_authed_table MACs
        discovered.Mac.Should().Be(mac);
        _mqttTransport.Verify(x => x.Publish("miio/command", It.Is<string>(s => s.Contains("set_properties") && s.Contains(did))), Times.Once);
    }

    [Fact]
    public void GetDevices_When_Any_Returns_DiscoveredDevicesList()
    {
        // Arrange
        var switchDid = _fixture.Create<string>();
        var thSensorDid = _fixture.Create<string>();
        var thMonitorDid = _fixture.Create<string>();
        var mac = "ffeeddccbbaa";

        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
            (thSensorDid, UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2300, 4500, 100]),
        ]);

        SetupBleDevices([
            (thMonitorDid, XiaomiBluetoothHygrothermograph2.PDID, mac),
        ]);

        // Act
        Gateway.DiscoverDevices();
        var devices = Gateway.GetDevices();

        // Assert
        devices.Count.Should().Be(3);
        devices.Any(x => x.DeviceId == switchDid && x.GetType() == typeof(XiaomiSmartWirelessSwitch)).Should().BeTrue();
        devices.Any(x => x.DeviceId == thSensorDid && x.GetType() == typeof(UnifiedTemperatureHumiditySensor)).Should().BeTrue();
        devices.Any(x => x.DeviceId == thMonitorDid && x.GetType() == typeof(XiaomiBluetoothHygrothermograph2)).Should().BeTrue();
    }

    [Fact]
    public void Discovered_Smoke_Detector_Can_Send_Commands()
    {
        var did = _fixture.Create<string>();
        SetupZigBeeDevices([(did, MiHomeLib.Devices.XiaomiHoneywellSmokeDetector.LUMI_MQTT_MODEL, [0, 150, 30])]);

        Gateway.DiscoverDevices();
        Gateway.GetDeviceByDid<MiHomeLib.Devices.XiaomiHoneywellSmokeDetector>(did).RunSelfTest();

        _mqttTransport.Verify(x => x.SendMessage(It.Is<string>(m => m.Contains(did) && m.Contains("14.1.85"))), Times.Once);
    }

    [Fact]
    public void Discovery_Continues_When_Gateway_Does_Not_Answer_For_One_Device()
    {
        var silentDid = _fixture.Create<string>();
        var answeringDid = _fixture.Create<string>();
        SetupZigBeeDevices([
            (silentDid, UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2363, 2902, 95]),
            (answeringDid, UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2363, 2902, 95]),
        ]);
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_device_prop") && s.Contains(silentDid))))
            .ThrowsAsync(new TimeoutException("Device '1.2.3.4' did not respond within 10 seconds"));

        Gateway.Invoking(x => x.DiscoverDevices()).Should().NotThrow();

        Gateway.GetDeviceByDid<UnifiedTemperatureHumiditySensor>(silentDid).Should().NotBeNull("it gets its values with its next report");
        Gateway.GetDeviceByDid<UnifiedTemperatureHumiditySensor>(answeringDid).Temperature.Should().Be(23.63f);
        _miioTransport.Verify(
            x => x.SendMessageAsync(It.Is<string>(s => s.Contains(silentDid))),
            Times.Once, "a silent gateway is not asked property by property");
    }

    [Fact]
    public void UnifiedTemperatureHumiditySensor_DiscoveryDoesNotFailWhenMiioResultIsMissing()
    {
        var did = _fixture.Create<string>();
        SetupZigBeeDevices([
            (did, UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2363, 2902, 95]),
        ]);
        _miioTransport
            .Setup(x => x.SendMessageAsync(
                It.Is<string>(request => request.Contains("get_device_prop") && request.Contains(did))))
            .ReturnsAsync("{\"code\":0}");

        Gateway.DiscoverDevices();

        var sensor = Gateway.GetDeviceByDid<UnifiedTemperatureHumiditySensor>(did);
        sensor.Should().NotBeNull();
        sensor.Temperature.Should().Be(0f, "the gateway returned no initial property values");
        _miioTransport.Verify(
            x => x.SendMessageAsync(
                It.Is<string>(request => request.Contains("get_device_prop") && request.Contains(did))),
            Times.Exactly(4),
            "after the combined response has no result array, each property is retried individually");
    }

    [Fact]
    public void GetDevices_When_NoOne_Returns_EmptyList()
    {
        // Act
        Gateway.DiscoverDevices();

        // Assert
        Gateway.GetDevices().Count.Should().Be(0);
    }

    [Fact]
    public void GetDeviceByDid_When_NotFound_Returns_Null()
    {
        // Arrange
        var did = _fixture.Create<string>();

        // Act
        Gateway.DiscoverDevices();

        // Assert
        Gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did).Should().BeNull();
    }

    [Fact]
    public void GetDeviceByDid_WhenExistsAndHasCorrectType_Returns_Device()
    {
        // Arrange
        var switchDid = _fixture.Create<string>();

        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
            (_fixture.Create<string>(), UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2300, 4500, 100]),
        ]);

        SetupBleDevices([
            (_fixture.Create<string>(), XiaomiBluetoothHygrothermograph2.PDID, "ffeeddccbbaa"),
        ]);

        // Act
        Gateway.DiscoverDevices();

        // Assert
        Gateway
            .GetDeviceByDid<XiaomiSmartWirelessSwitch>(switchDid)
                .Should()
                .NotBeNull()
                .And
                .Match<XiaomiSmartWirelessSwitch>(x => x.DeviceId == switchDid);
    }

    [Fact]
    public void GetDeviceByDid_WhenHasWrongType_Returns_Null()
    {
        // Arrange
        var switchDid = _fixture.Create<string>();

        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL, [3032, 100, 192, 88]),
            (_fixture.Create<string>(), UnifiedTemperatureHumiditySensor.LUMI_MQTT_MODEL, [2300, 4500, 100]),
        ]);

        SetupBleDevices([
            (_fixture.Create<string>(), XiaomiBluetoothHygrothermograph2.PDID, "ffeeddccbbaa"),
        ]);

        // Act
        Gateway.DiscoverDevices();

        // Assert
        Gateway.GetDeviceByDid<UnifiedTemperatureHumiditySensor>(switchDid).Should().BeNull();
    }
}
