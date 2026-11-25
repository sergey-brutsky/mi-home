using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeEmulator;
using MiHomeLib.Devices;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class Gateway2IntegrationTests
{
    private const string GatewaySid = "0123456789abcdef0123456789abcdef";
    private const string GatewayToken = "0123456789abcdef";
    private const string DeveloperPassword = "fedcba9876543210";

    [Fact(Timeout = 30_000)]
    public async Task Discovery_reads_three_devices_over_udp()
    {
        var emulator = CreateGateway2Emulator();
        await using var ownedEmulator = emulator;
        await using var gateway = CreateGateway(emulator);
        gateway.ReadDeviceInterval = TimeSpan.Zero;
        gateway.ReadAnswerTimeout = TimeSpan.FromMilliseconds(100);

        var discovered = new List<GatewaySubDeviceBase>();
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            lock (discovered) discovered.Add(device);
            return Task.CompletedTask;
        };

        await gateway.DiscoverDevicesAsync();

        Assert.Equal(3, discovered.Count);
        Assert.Equal(3, gateway.GetDevices().Count);
        Assert.Equal(23.8f, gateway.GetDeviceById<XiaomiTemperatureHumiditySensor>("sensor-ht")!.Temperature);
        Assert.Equal(41.2f, gateway.GetDeviceById<XiaomiTemperatureHumiditySensor>("sensor-ht")!.Humidity);
        Assert.Equal(XiaomiMiSmartPowerPlugCN.PlugState.On,
            gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>("plug")!.State);
    }

    [Fact(Timeout = 30_000)]
    public async Task Report_updates_device_and_raises_gateway_event()
    {
        var emulator = CreateGateway2Emulator();
        await using var ownedEmulator = emulator;
        await using var gateway = CreateGateway(emulator);
        gateway.ReadDeviceInterval = TimeSpan.Zero;
        await gateway.DiscoverDevicesAsync();

        var events = Channel.CreateUnbounded<DeviceEvent>();
        gateway.OnDeviceEventAsync += deviceEvent =>
        {
            events.Writer.TryWrite(deviceEvent);
            return Task.CompletedTask;
        };

        await emulator.SendReportAsync("plug", new JsonObject { ["load_power"] = "15.5" });
        var reportedEvent = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("OnLoadPowerChangeAsync", reportedEvent.EventName);
        Assert.Equal(15.5f, gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>("plug")!.LoadPower);
    }

    [Fact(Timeout = 30_000)]
    public async Task Write_uses_developer_key_for_current_gateway_token()
    {
        var emulator = CreateGateway2Emulator();
        await using var ownedEmulator = emulator;
        await using var gateway = CreateGateway(emulator);
        gateway.ReadDeviceInterval = TimeSpan.Zero;
        await gateway.DiscoverDevicesAsync();

        var plug = gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>("plug")!;
        await plug.PowerOnAsync();
        var firstWrite = await emulator.ReceivedWrites.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(firstWrite.KeyIsValid);

        await emulator.SendHeartbeatAsync("1111111111111111");
        await Task.Delay(50);
        await plug.PowerOffAsync();
        var secondWrite = await emulator.ReceivedWrites.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(secondWrite.KeyIsValid);
    }

    [Fact(Timeout = 30_000)]
    public async Task Gateway_becomes_unavailable_after_silence()
    {
        var emulator = CreateGateway2Emulator();
        await using var ownedEmulator = emulator;
        await using var gateway = CreateGateway(emulator);
        gateway.ReadDeviceInterval = TimeSpan.Zero;
        gateway.GatewayAvailabilityTimeout = TimeSpan.FromMilliseconds(40);
        gateway.AvailabilityCheckInterval = TimeSpan.FromMilliseconds(5);

        await gateway.DiscoverDevicesAsync();
        Assert.True(gateway.IsAvailable);
        await WaitUntilAsync(() => gateway.IsAvailable == false, TimeSpan.FromSeconds(5));
        Assert.False(gateway.IsAvailable);
    }

    [Fact(Timeout = 30_000)]
    public async Task Discovery_can_be_cancelled_while_gateway_is_silent()
    {
        var emulator = CreateGateway2Emulator();
        emulator.RespondToRequests = false;
        await using var ownedEmulator = emulator;
        await using var gateway = CreateGateway(emulator);
        gateway.DiscoveryTimeout = TimeSpan.FromSeconds(10);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.DiscoverDevicesAsync(cancellation.Token));
    }

    private static Gateway2Emulator CreateGateway2Emulator() =>
        new(GatewaySid, GatewayToken, DeveloperPassword,
        [
            new Gateway2Device("magnet", "magnet", 11, new JsonObject { ["voltage"] = 3005, ["status"] = "close" }),
            new Gateway2Device("sensor-ht", "sensor_ht", 12, new JsonObject { ["voltage"] = 3005, ["temperature"] = "2380", ["humidity"] = "4120" }),
            new Gateway2Device("plug", "plug", 13, new JsonObject { ["voltage"] = 3600, ["status"] = "on", ["load_power"] = "0" }),
        ]);

    private static XiaomiGateway2 CreateGateway(Gateway2Emulator emulator)
    {
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var transport = new UdpTransport(socket, emulator.EndPoint, NullLoggerFactory.Instance);
        var miio = new MiioTransport("127.0.0.1", emulator.MiioToken, emulator.MiioPort);
        return new XiaomiGateway2(miio, transport, emulator.Sid, 0, NullLoggerFactory.Instance);
    }

    internal static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Condition was not met before the timeout.");
            await Task.Delay(10);
        }
    }
}
