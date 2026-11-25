using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeLib.XiaomiGateway2;
using MiHomeUnitTests.Gateways.Gateway2;
using MiHomeUnitTests.Gateways.Mqtt;
using Xunit;

namespace MiHomeUnitTests.Gateways;

public sealed class Gateway2AvailabilityTests : Gateway2TestBase
{
    private void RaiseChildReport(string sid, string model, string data) =>
        _messageTransport.Raise(x => x.OnMessageReceived += null,
            ReadAck(sid, model, data).Replace("read_ack", "report"));

    [Fact]
    public async Task Device_Becomes_Unavailable_Once_And_A_Report_Restores_It()
    {
        const string sid = "availability-magnet";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "magnet", "{\"status\":\"close\"}"), 1);
        var deviceEvents = new ConcurrentQueue<DeviceEvent>();
        _gateway.OnDeviceEventAsync += deviceEvent =>
        {
            deviceEvents.Enqueue(deviceEvent);
            return Task.CompletedTask;
        };
        await _gateway.DiscoverDevicesAsync();

        var device = _gateway.GetDevices().Should().ContainSingle().Which;
        device.AvailabilityTimeout.Should().Be(TimeSpan.FromHours(3));
        var changes = new ConcurrentQueue<bool>();
        device.OnAvailabilityChangedAsync += available =>
        {
            changes.Enqueue(available);
            return Task.CompletedTask;
        };
        var now = DateTime.UtcNow.AddHours(3).AddSeconds(1);
        _gateway.UtcNow = () => now;

        _gateway.CheckAvailability();
        _gateway.CheckAvailability();
        changes.Should().Equal(false);
        device.IsAvailable.Should().BeFalse();

        now = DateTime.UtcNow;
        RaiseChildReport(sid, "magnet", "{\"status\":\"open\"}");
        await WaitForMessageAsync(() => device.IsAvailable && changes.Count == 2);

        changes.Should().Equal(false, true);
        deviceEvents.Should().Contain(deviceEvent =>
            deviceEvent.EventName == nameof(GatewaySubDeviceBase.OnAvailabilityChangedAsync) &&
            deviceEvent.Arguments.SequenceEqual(new object[] { true }));
    }

    [Fact]
    public async Task Null_Device_Timeout_Disables_Availability_Checks()
    {
        const string sid = "no-timeout-magnet";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "magnet", "{\"status\":\"close\"}"), 1);
        await _gateway.DiscoverDevicesAsync();
        var device = _gateway.GetDevices().Single();
        device.AvailabilityTimeout = null;
        _gateway.UtcNow = () => DateTime.UtcNow.AddDays(10);

        _gateway.CheckAvailability();

        device.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Availability_Timeout_Can_Be_Set_Before_Protocol_Initialization()
    {
        using var device = new XiaomiTemperatureHumiditySensor("manual-sensor", 0, _loggerFactory)
        {
            AvailabilityTimeout = TimeSpan.FromMinutes(5)
        };

        device.InitializeProtocol(DeviceProtocol.LumiMqtt, XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL);

        device.AvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_Availability_Timeout_Is_Rejected(long ticks)
    {
        using var device = new XiaomiTemperatureHumiditySensor("manual-sensor", 0, _loggerFactory);
        Action setTimeout = () => device.AvailabilityTimeout = TimeSpan.FromTicks(ticks);

        setTimeout.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public sealed class MqttAvailabilityTests : MqttGatewayTestBase
{
    private static async Task WaitForMessageAsync(Func<bool> processed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!processed()) await Task.Delay(1, timeout.Token);
    }

    [Fact]
    public async Task Mqtt_Device_Timeout_Uses_Time_Added_To_Gateway()
    {
        const string did = "availability-plug";
        SetupZigBeeDevices([(did, XiaomiMiSmartPowerPlugCN.LUMI_MQTT_MODEL, [0, 0, 1800, 120, 36])]);
        await Gateway.DiscoverDevicesAsync();
        var device = Gateway.GetDevices().Single();
        var changed = new ConcurrentQueue<bool>();
        device.OnAvailabilityChangedAsync += available =>
        {
            changed.Enqueue(available);
            return Task.CompletedTask;
        };
        var now = DateTime.UtcNow.AddHours(3).AddSeconds(1);
        Gateway.UtcNow = () => now;

        Gateway.CheckAvailability();
        await WaitForMessageAsync(() => changed.Count == 1);

        device.IsAvailable.Should().BeFalse();
        changed.Should().Equal(false);
    }

    [Fact]
    public async Task Ble_Device_Availability_Checking_Is_Disabled_By_Default()
    {
        SetupBleDevices([("availability-ble", XiaomiBluetoothHygrothermograph2.PDID, "ffeeddccbbaa")]);

        await Gateway.DiscoverDevicesAsync();

        Gateway.GetDevices().Should().ContainSingle().Which.AvailabilityTimeout.Should().BeNull();
    }
}

public sealed class Gateway2GatewayAvailabilityTests : Gateway2TestBase
{
    [Fact]
    public async Task Read_Ack_Initializes_Gateway_Availability_And_Other_Sid_Does_Not_Restore_It()
    {
        _gateway.IsAvailable.Should().BeNull();
        SetupIdList();
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        var changes = new ConcurrentQueue<bool>();
        _gateway.OnAvailabilityChangedAsync += available =>
        {
            changes.Enqueue(available);
            return Task.CompletedTask;
        };

        await _gateway.DiscoverDevicesAsync();

        _gateway.IsAvailable.Should().BeTrue();
        changes.Should().BeEmpty();

        _gateway.UtcNow = () => DateTime.UtcNow.AddMinutes(1).AddSeconds(1);
        _gateway.CheckAvailability();
        _gateway.IsAvailable.Should().BeFalse();
        changes.Should().Equal(false);

        RaiseGatewayMessage("heartbeat", "other-gateway", "{}");
        await Task.Delay(20);
        _gateway.IsAvailable.Should().BeFalse();
        changes.Should().Equal(false);

        RaiseGatewayMessage("heartbeat", _gatewaySid, "{}");
        await WaitForMessageAsync(() => _gateway.IsAvailable == true && changes.Count == 2);
        changes.Should().Equal(false, true);
    }
}
