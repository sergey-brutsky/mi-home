using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeEmulator;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RecordedScenarioIntegrationTests
{
    [Fact(Timeout = 30_000)]
    public async Task Recorded_fixtures_play_without_logged_errors()
    {
        var loggerFactory = new ErrorRecordingLoggerFactory();
        await using var gatewayEmulator = new Gateway2Emulator(
            "0123456789abcdef0123456789abcdef",
            "0123456789abcdef",
            "fedcba9876543210");
        await using var gateway2 = CreateGateway2(gatewayEmulator, loggerFactory);
        gateway2.ReadDeviceInterval = TimeSpan.Zero;
        await gateway2.DiscoverDevicesAsync();

        await using var miioEmulator = new MiioEmulator("0123456789abcdef0123456789abcdef");
        await using var broker = new MqttBrokerEmulator();
        using var mqttTransport = new MqttTransport(
            "127.0.0.1", broker.Port, ["zigbee/send"], "zigbee/recv", loggerFactory);
        await using var mqttGateway = new MultimodeGateway(
            "", new MiioTransport("127.0.0.1", "0123456789abcdef0123456789abcdef", miioEmulator.Port),
            mqttTransport, new FixedDevicesDiscoverer(), loggerFactory);
        await mqttGateway.DiscoverDevicesAsync();

        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recorded");
        var scenario = RecordedScenario.LoadDirectory(fixtures);
        await scenario.PlayAsync(gatewayEmulator, broker);
        await Gateway2IntegrationTests.WaitUntilAsync(() => gateway2.GetDevices().Count >= 6, TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        Assert.Empty(loggerFactory.Errors);
    }

    private static XiaomiGateway2 CreateGateway2(Gateway2Emulator emulator, ErrorRecordingLoggerFactory loggerFactory)
    {
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var transport = new UdpTransport(socket, emulator.EndPoint, loggerFactory);
        var miio = new MiioTransport("127.0.0.1", emulator.MiioToken, emulator.MiioPort);
        return new XiaomiGateway2(miio, transport, emulator.Sid, 0, loggerFactory);
    }
}
