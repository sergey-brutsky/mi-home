using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using MiHomeUnitTests.Gateways.Gateway2;
using MiHomeUnitTests.Gateways.Mqtt;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Gateways;

public sealed class Gateway2AsyncDisposeTests : Gateway2TestBase
{
    [Fact]
    public async Task DisposeAsync_Waits_For_Active_Device_Event_Handlers_And_Stops_New_Events()
    {
        const string sid = "async-dispose-magnet";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "magnet", "{\"status\":\"close\"}"), 1);
        await _gateway.DiscoverDevicesAsync();

        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothEventsEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventCount = 0;
        _gateway.OnDeviceEventAsync += async _ =>
        {
            if (Interlocked.Increment(ref eventCount) == 2)
                bothEventsEntered.TrySetResult(true);
            entered.TrySetResult(true);
            await resume.Task;
        };
        RaiseReport(sid, "magnet", "{\"status\":\"open\"}");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await bothEventsEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var eventCountBeforeDispose = Volatile.Read(ref eventCount);
        var disposal = _gateway.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        resume.TrySetResult(true);
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));

        RaiseReport(sid, "magnet", "{\"status\":\"close\"}");
        await Task.Delay(20);
        Volatile.Read(ref eventCount).Should().Be(eventCountBeforeDispose);
    }

    [Fact]
    public async Task DisposeAsync_Called_From_Device_Event_Handler_Does_Not_Wait_For_Itself()
    {
        const string sid = "async-dispose-from-handler";
        SetupIdList(sid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers(sid, ReadAck(sid, "magnet", "{\"status\":\"close\"}"), 1);
        await _gateway.DiscoverDevicesAsync();

        var disposalCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnDeviceEventAsync += async _ =>
        {
            await _gateway.DisposeAsync();
            disposalCompleted.TrySetResult(true);
        };
        RaiseReport(sid, "magnet", "{\"status\":\"open\"}");

        await disposalCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _gateway.DisposeAsync();
        _gateway.Dispose();
    }

    private void RaiseReport(string sid, string model, string data) =>
        _messageTransport.Raise(x => x.OnMessageReceived += null,
            ReadAck(sid, model, data).Replace("read_ack", "report"));
}

public sealed class MqttAsyncDisposeTests : MqttGatewayTestBase
{
    [Fact]
    public async Task DisposeAsync_Waits_For_Device_Event_Handler_And_Stops_New_Events()
    {
        const string did = "async-dispose-plug";
        SetupZigBeeDevices([(did, MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN.LUMI_MQTT_MODEL, [0, 0, 1800, 120, 36])]);
        await Gateway.DiscoverDevicesAsync();

        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventCount = 0;
        Gateway.OnDeviceEventAsync += async _ =>
        {
            Interlocked.Increment(ref eventCount);
            entered.TrySetResult(true);
            await resume.Task;
        };
        RaiseZigbeeReportEvent(did, 1794564562637, [("4.1.85", 1)]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var disposal = Gateway.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        resume.TrySetResult(true);
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));

        RaiseZigbeeReportEvent(did, 1794564562638, [("4.1.85", 0)]);
        await Task.Delay(20);
        Volatile.Read(ref eventCount).Should().Be(1);
    }

}

public sealed class MqttConnectionAsyncDisposeTests : MiHomeUnitTests.Devices.Mqtt.MqttDeviceTestBase
{
    private readonly MqttGatewayBase _gateway;
    private readonly Mock<IMqttConnectionEvents> _connectionEvents;

    public MqttConnectionAsyncDisposeTests()
    {
        _connectionEvents = _mqttTransport.As<IMqttConnectionEvents>();
        _gateway = new MultimodeGateway("gateway", _miioTransport.Object, _mqttTransport.Object,
            _devicesDiscoverer.Object, _loggerFactory);
    }

    public override void Dispose()
    {
        _gateway.Dispose();
        base.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_Waits_For_Connection_Event_And_Is_Idempotent()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnConnectionLostAsync += async () =>
        {
            entered.TrySetResult(true);
            await resume.Task;
        };
        _connectionEvents.Raise(transport => transport.ConnectionLost += null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var disposal = _gateway.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        resume.TrySetResult(true);
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));
        await _gateway.DisposeAsync();
        _gateway.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_Called_From_Connection_Handler_Does_Not_Wait_For_Itself()
    {
        _mqttTransport.As<IMqttConnectionEvents>();
        var disposalCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnConnectionLostAsync += async () =>
        {
            await _gateway.DisposeAsync();
            disposalCompleted.TrySetResult(true);
        };

        _connectionEvents.Raise(transport => transport.ConnectionLost += null);

        await disposalCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisposeAsync_From_Another_Gateways_Handler_Waits_For_That_Gateways_Handlers()
    {
        var otherTransport = new Mock<IMqttTransport>();
        var otherConnectionEvents = otherTransport.As<IMqttConnectionEvents>();
        var otherGateway = new MultimodeGateway("other-gateway", _miioTransport.Object, otherTransport.Object,
            _devicesDiscoverer.Object, _loggerFactory);
        var otherHandlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeOtherHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        otherGateway.OnConnectionLostAsync += async () =>
        {
            otherHandlerEntered.TrySetResult(true);
            await resumeOtherHandler.Task;
        };
        otherConnectionEvents.Raise(transport => transport.ConnectionLost += null);
        await otherHandlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var otherDisposalStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnConnectionLostAsync += async () =>
        {
            var otherDisposal = otherGateway.DisposeAsync().AsTask();
            otherDisposalStarted.TrySetResult(true);
            await otherDisposal;
            handlerCompleted.TrySetResult(true);
        };
        _connectionEvents.Raise(transport => transport.ConnectionLost += null);
        await otherDisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        handlerCompleted.Task.IsCompleted.Should().BeFalse();

        resumeOtherHandler.TrySetResult(true);
        await handlerCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _gateway.DisposeAsync();
        otherGateway.Dispose();
    }
}
