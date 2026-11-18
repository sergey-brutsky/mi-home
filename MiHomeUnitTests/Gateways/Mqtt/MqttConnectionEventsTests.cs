using System;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using MiHomeUnitTests.Devices.Mqtt;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Gateways.Mqtt;

public class MqttConnectionEventsTests : MqttDeviceTestBase
{
    private MqttGatewayBase Gateway { get; }

    public MqttConnectionEventsTests()
    {
        _mqttTransport.As<IMqttConnectionEvents>();
        Gateway = new MultimodeGateway("gateway", _miioTransport.Object, _mqttTransport.Object,
            _devicesDiscoverer.Object, _loggerFactory);
    }

    public override void Dispose()
    {
        Gateway.Dispose();
        base.Dispose();
    }

    [Fact]
    public async Task Connection_Events_Are_Forwarded_Without_Rediscovering_Devices_And_Unsubscribed_On_Disposal()
    {
        var events = _mqttTransport.As<IMqttConnectionEvents>();
        var lost = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lostCount = 0;
        var restoredCount = 0;
        Gateway.OnConnectionLostAsync += () => { lostCount++; lost.TrySetResult(true); return Task.CompletedTask; };
        Gateway.OnConnectionRestoredAsync += () => { restoredCount++; restored.TrySetResult(true); return Task.CompletedTask; };

        events.Raise(transport => transport.ConnectionLost += null);
        events.Raise(transport => transport.ConnectionRestored += null);
        await Task.WhenAll(lost.Task, restored.Task).WaitAsync(TimeSpan.FromSeconds(2));

        lostCount.Should().Be(1);
        restoredCount.Should().Be(1);
        _devicesDiscoverer.Verify(discoverer => discoverer.DiscoverZigBeeDevices(), Times.Never);
        _devicesDiscoverer.Verify(discoverer => discoverer.DiscoverBleDevices(), Times.Never);
        _devicesDiscoverer.Verify(discoverer => discoverer.DiscoverMeshDevices(), Times.Never);
        Gateway.Dispose();
        events.VerifyRemove(transport => transport.ConnectionLost -= It.IsAny<Action>(), Times.Once);
        events.VerifyRemove(transport => transport.ConnectionRestored -= It.IsAny<Action>(), Times.Once);
        events.Raise(transport => transport.ConnectionLost += null);
        events.Raise(transport => transport.ConnectionRestored += null);
        lostCount.Should().Be(1);
        restoredCount.Should().Be(1);
    }

    [Fact]
    public async Task Failing_And_Slow_Connection_Subscribers_Do_Not_Block_Other_Subscribers()
    {
        var events = _mqttTransport.As<IMqttConnectionEvents>();
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("subscriber failed");
        Gateway.OnConnectionLostAsync += () => throw failure;
        Gateway.OnConnectionLostAsync += () => resume.Task;
        Gateway.OnConnectionLostAsync += () => { received.TrySetResult(true); return Task.CompletedTask; };
        try
        {
            events.Raise(transport => transport.ConnectionLost += null);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
            _loggerFactory.TakeErrors().Should().ContainSingle().Which.Should().BeSameAs(failure);
        }
        finally { resume.TrySetResult(true); }
    }
}
