using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway.Discovery;
using MiHomeUnitTests.Gateways.Gateway2;
using MiHomeUnitTests.Gateways.Mqtt;
using Moq;
using Xunit;
using XiaomiTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;

namespace MiHomeUnitTests.Gateways;

public sealed class Gateway2CancellationTests : Gateway2TestBase
{
    [Fact]
    public async Task Cancellation_While_Waiting_For_Id_List_Allows_A_Later_Discovery()
    {
        _gateway.DiscoveryTimeout = TimeSpan.FromSeconds(5);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        Func<Task> canceledDiscovery = () => _gateway.DiscoverDevicesAsync(cancellation.Token);

        await canceledDiscovery.Should().ThrowAsync<OperationCanceledException>();
        elapsed.Elapsed.Should().BeLessThan(_gateway.DiscoveryTimeout);

        SetupIdList();
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        await _gateway.DiscoverDevicesAsync();
    }

    [Fact]
    public async Task Cancellation_During_Device_Read_Clears_Reads_And_Allows_A_Retry()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readAttempt = 0;
        _gateway.ReadAttempts = 2;
        _gateway.ReadAnswerTimeout = TimeSpan.FromSeconds(5);
        SetupIdList("slow-device");
        _messageTransport.Setup(x => x.SendRead(_gatewaySid))
            .Callback(() => _messageTransport.Raise(x => x.OnMessageReceived += null, ReadAck(_gatewaySid, "gateway", "{}")));
        _messageTransport.Setup(x => x.SendRead("slow-device")).Callback(() =>
        {
            if (Interlocked.Increment(ref readAttempt) == 1)
                readStarted.TrySetResult();
            else
                _messageTransport.Raise(x => x.OnMessageReceived += null, ReadAck("slow-device", "sensor_ht", "{\"temperature\":\"2200\"}"));
        });

        using var cancellation = new CancellationTokenSource();
        var discovery = _gateway.DiscoverDevicesAsync(cancellation.Token);
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> canceledDiscovery = async () => await discovery;
        await canceledDiscovery.Should().ThrowAsync<OperationCanceledException>();
        _gateway.AwaitedReadCount.Should().Be(0);

        await _gateway.DiscoverDevicesAsync();
        _gateway.GetDeviceById<XiaomiTemperatureHumiditySensor>("slow-device").Temperature.Should().Be(22f);
    }
}

public sealed class MqttCancellationTests : MqttGatewayTestBase
{
    [Fact]
    public async Task Async_Discoverer_Cancellation_Cleans_Up_And_Allows_A_Retry()
    {
        var discoverer = new Mock<IDevicesDiscoverer>();
        var asyncDiscoverer = discoverer.As<IAsyncDevicesDiscoverer>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        asyncDiscoverer.Setup(value => value.DiscoverZigBeeDevicesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => Interlocked.Increment(ref calls) == 1
                ? WaitUntilCanceled(token)
                : Task.FromResult(new List<ZigBeeDeviceInfo>()));
        asyncDiscoverer.Setup(value => value.DiscoverBleDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BleDeviceInfo>());
        asyncDiscoverer.Setup(value => value.DiscoverMeshDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MeshDeviceInfo>());
        using var gateway = new Mock<MiHomeLib.MqttGateway.MqttGatewayBase>(
            _gwDid, _miioTransport.Object, _mqttTransport.Object, discoverer.Object, _loggerFactory)
        { CallBase = true }.Object;

        using var cancellation = new CancellationTokenSource();
        var discovery = gateway.DiscoverDevicesAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> canceledDiscovery = async () => await discovery;
        await canceledDiscovery.Should().ThrowAsync<OperationCanceledException>();
        gateway.InitializingDeviceCount.Should().Be(0);

        await gateway.DiscoverDevicesAsync();
        calls.Should().Be(2);

        async Task<List<ZigBeeDeviceInfo>> WaitUntilCanceled(CancellationToken token)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return [];
        }
    }
}
