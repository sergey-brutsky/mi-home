using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeEmulator;
using MiHomeLib.Devices;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.MqttGateway;
using MiHomeLib.MqttGateway.Discovery;
using Xunit;

namespace MiHomeUnitTests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MqttIntegrationTests
{
    private const string MiioToken = "0123456789abcdef0123456789abcdef";
    private const string Did = "lumi.158d000000001234";

    [Fact(Timeout = 30_000)]
    public async Task Discovery_report_and_write_use_real_miio_and_mqtt_transports()
    {
        await using var miio = CreateMiioEmulator();
        await using var broker = new MqttBrokerEmulator();
        using var mqtt = CreateMqttTransport(broker);
        await using var gateway = CreateGateway(miio, mqtt);
        var discovered = new TaskCompletionSource<GatewaySubDeviceBase>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered.TrySetResult(device);
            return Task.CompletedTask;
        };
        var events = Channel.CreateUnbounded<DeviceEvent>();
        gateway.OnDeviceEventAsync += deviceEvent =>
        {
            events.Writer.TryWrite(deviceEvent);
            return Task.CompletedTask;
        };

        await gateway.DiscoverDevicesAsync();
        var device = Assert.IsType<XiaomiMiSmartPowerPlugCN>(
            await discovered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(miio.ReceivedRequests, request => request["method"]?.GetValue<string>() == "get_device_prop");

        await broker.PublishAsync("zigbee/send",
            $"{{\"cmd\":\"report\",\"did\":\"{Did}\",\"time\":1794564562637,\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}");
        var deviceEvent = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("OnStateChangeAsync", deviceEvent.EventName);
        Assert.Equal(XiaomiMiSmartPowerPlugCN.PlugState.On, device.State);

        await device.PowerOffAsync();
        var write = await broker.Received.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("zigbee/recv", write.Topic);
        var writeJson = JsonNode.Parse(write.Payload)!.AsObject();
        Assert.Equal("write", writeJson["cmd"]!.GetValue<string>());
        Assert.Equal(Did, writeJson["did"]!.GetValue<string>());
    }

    [Fact(Timeout = 30_000)]
    public async Task Broker_disconnect_raises_events_reconnects_and_receives_reports()
    {
        await using var miio = CreateMiioEmulator();
        await using var broker = new MqttBrokerEmulator();
        using var mqtt = CreateMqttTransport(broker);
        mqtt.ReconnectDelays = [TimeSpan.FromMilliseconds(50)];
        await using var gateway = CreateGateway(miio, mqtt);

        var connectionLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionRestored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.OnConnectionLostAsync += () =>
        {
            connectionLost.TrySetResult();
            return Task.CompletedTask;
        };
        gateway.OnConnectionRestoredAsync += () =>
        {
            connectionRestored.TrySetResult();
            return Task.CompletedTask;
        };
        await gateway.DiscoverDevicesAsync();
        var plug = gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>(Did);
        Assert.NotNull(plug);

        await broker.DropConnectionsAsync();
        await connectionLost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await connectionRestored.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Gateway2IntegrationTests.WaitUntilAsync(() => broker.ConnectedClientCount > 0, TimeSpan.FromSeconds(5));

        var stateChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        plug!.OnStateChangeAsync += () =>
        {
            stateChanged.TrySetResult();
            return Task.CompletedTask;
        };
        await broker.PublishAsync("zigbee/send",
            $"{{\"cmd\":\"report\",\"did\":\"{Did}\",\"time\":1794564562637,\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}");
        await stateChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(XiaomiMiSmartPowerPlugCN.PlugState.On, plug.State);
    }

    private static MiioEmulator CreateMiioEmulator()
    {
        var emulator = new MiioEmulator(MiioToken)
        {
            Handler = request =>
            {
                if (request["method"]?.GetValue<string>() != "get_device_prop")
                    return new JsonObject
                    {
                        ["life"] = 1,
                        ["miio_ver"] = "0.0.0",
                        ["fw_ver"] = "1.0.0",
                        ["mac"] = "00:00:00:00:00:00",
                        ["hw_ver"] = "emulator",
                        ["ap"] = new JsonObject
                        {
                            ["ssid"] = "loopback",
                            ["bssid"] = "00:00:00:00:00:00",
                            ["rssi"] = 0,
                            ["freq"] = 0,
                        },
                        ["netif"] = new JsonObject
                        {
                            ["localIp"] = "127.0.0.1",
                            ["mask"] = "255.0.0.0",
                            ["gw"] = "127.0.0.1",
                        },
                        ["model"] = "miio.emulator",
                    };
                var parameters = request["params"] as JsonArray ?? [];
                var values = new JsonArray();
                for (var index = 1; index < parameters.Count; index++) values.Add(0);
                return new JsonObject { ["code"] = 0, ["result"] = values };
            },
        };
        return emulator;
    }

    private static MqttTransport CreateMqttTransport(MqttBrokerEmulator broker) =>
        new("127.0.0.1", broker.Port, ["zigbee/send"], "zigbee/recv", NullLoggerFactory.Instance,
            operationTimeout: TimeSpan.FromSeconds(2), pingInterval: TimeSpan.FromSeconds(5),
            pingResponseTimeout: TimeSpan.FromSeconds(2));

    private static MultimodeGateway CreateGateway(MiioEmulator miio, MqttTransport mqtt) =>
        new(Did, new MiioTransport("127.0.0.1", MiioToken, miio.Port), mqtt,
            new FixedDevicesDiscoverer([new ZigBeeDeviceInfo(Did, "lumi.plug")]), NullLoggerFactory.Instance);
}
