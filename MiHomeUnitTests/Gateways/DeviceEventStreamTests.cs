using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;
using MiHomeUnitTests.Gateways.Gateway2;
using MiHomeUnitTests.Gateways.Mqtt;
using Xunit;

namespace MiHomeUnitTests.Gateways;

public sealed class Gateway2DeviceEventStreamTests : Gateway2TestBase
{
    private void RaiseChildReport(string sid, string model, string data) =>
        _messageTransport.Raise(x => x.OnMessageReceived += null,
            ReadAck(sid, model, data).Replace("read_ack", "report"));

    [Fact]
    public async Task Discovery_And_Reports_Publish_Device_Events_But_Initial_Read_Does_Not()
    {
        const string sid = "event-magnet";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "magnet", "{\"status\":\"close\"}"), 1);
        var events = new ConcurrentQueue<DeviceEvent>();
        _gateway.OnDeviceEventAsync += deviceEvent =>
        {
            events.Enqueue(deviceEvent);
            return Task.CompletedTask;
        };

        await _gateway.DiscoverDevicesAsync();
        events.Should().BeEmpty();

        var device = _gateway.GetDevices().Should().ContainSingle().Which;
        RaiseChildReport(sid, "magnet", "{\"status\":\"open\"}");
        await WaitForMessageAsync(() => events.Count == 2);

        events.Select(deviceEvent => deviceEvent.EventName).Should().Equal(
            nameof(WindowDoorSensorBase.OnOpenAsync),
            nameof(WindowDoorSensorBase.OnContactChangedAsync));
        events.Should().OnlyContain(deviceEvent => ReferenceEquals(deviceEvent.Device, device));
    }

    [Fact]
    public async Task Gateway2_Value_Event_Contains_Previous_Value()
    {
        const string sid = "event-temperature";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "sensor_ht", "{\"voltage\":3025}"), 1);
        var events = new ConcurrentQueue<DeviceEvent>();
        _gateway.OnDeviceEventAsync += deviceEvent =>
        {
            events.Enqueue(deviceEvent);
            return Task.CompletedTask;
        };

        await _gateway.DiscoverDevicesAsync();
        RaiseChildReport(sid, "sensor_ht", "{\"voltage\":3030}");
        await WaitForMessageAsync(() => events.Count == 1);

        var deviceEvent = events.Single();
        deviceEvent.EventName.Should().Be(nameof(LumiBatteryDeviceBase.OnVoltageChangeAsync));
        deviceEvent.Arguments.Should().Equal(3.025f);
    }

    [Fact]
    public async Task Gateway_Event_Subscriber_Failure_Does_Not_Stop_Device_Events_Or_Later_Reports()
    {
        const string sid = "faulty-event-sensor";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "sensor_ht", "{\"temperature\":\"2000\"}"), 1);
        await _gateway.DiscoverDevicesAsync();
        var device = _gateway.GetDevices().OfType<XiaomiTemperatureHumiditySensor>().Single();
        var deviceCalls = 0;
        var gatewayCalls = 0;
        device.OnTemperatureChangeAsync += _ =>
        {
            Interlocked.Increment(ref deviceCalls);
            return Task.CompletedTask;
        };
        _gateway.OnDeviceEventAsync += _ =>
        {
            if (Interlocked.Increment(ref gatewayCalls) == 1)
                return Task.FromException(new InvalidOperationException("event subscriber failed"));
            return Task.CompletedTask;
        };

        RaiseChildReport(sid, "sensor_ht", "{\"temperature\":\"2100\"}");
        RaiseChildReport(sid, "sensor_ht", "{\"temperature\":\"2200\"}");
        await WaitForMessageAsync(() => Volatile.Read(ref deviceCalls) == 2 && Volatile.Read(ref gatewayCalls) == 2);

        _loggerFactory.TakeErrors().Should().ContainSingle()
            .Which.Message.Should().Be("event subscriber failed");
    }
}

public sealed class MqttDeviceEventStreamTests : MqttGatewayTestBase
{
    private static async Task WaitForMessageAsync(Func<bool> processed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!processed()) await Task.Delay(1, timeout.Token);
    }

    [Fact]
    public async Task Mqtt_Plug_State_Changes_Appear_In_Gateway_Event_Stream()
    {
        const string did = "event-plug";
        SetupZigBeeDevices([(did, XiaomiMiSmartPowerPlugCN.LUMI_MQTT_MODEL, [0, 0, 1800, 120, 36])]);
        var events = new ConcurrentQueue<DeviceEvent>();
        Gateway.OnDeviceEventAsync += deviceEvent =>
        {
            events.Enqueue(deviceEvent);
            return Task.CompletedTask;
        };

        await Gateway.DiscoverDevicesAsync();
        RaiseZigbeeReportEvent(did, 1794564562637, [("4.1.85", 1)]);
        await WaitForMessageAsync(() => events.Count == 1);

        events.Single().EventName.Should().Be(nameof(XiaomiMiSmartPowerPlugCN.OnStateChangeAsync));
        events.Single().Device.Should().BeOfType<XiaomiMiSmartPowerPlugCN>();
    }

    [Fact]
    public async Task Honeywell_Sensitivity_Is_Published_Only_Once()
    {
        const string did = "event-smoke-sensor";
        SetupZigBeeDevices([(did, XiaomiHoneywellSmokeDetector.LUMI_MQTT_MODEL, [3, 150, 30])]);
        var events = new ConcurrentQueue<DeviceEvent>();
        Gateway.OnDeviceEventAsync += deviceEvent =>
        {
            events.Enqueue(deviceEvent);
            return Task.CompletedTask;
        };

        await Gateway.DiscoverDevicesAsync();
        RaiseZigbeeReportEvent(did, 1794564562637, [("14.1.85", (int)XiaomiHoneywellSmokeDetector.SensitivityMode.LowSmoke)]);
        await WaitForMessageAsync(() => events.Count == 1);

        events.Should().ContainSingle().Which.EventName
            .Should().Be(nameof(XiaomiHoneywellSmokeDetector.OnSmokeSensitivityModeChangedAsync));
    }
}
