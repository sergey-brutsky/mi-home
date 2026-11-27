using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MiHomeEmulator;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.MqttGateway;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeUnitTests.Integration;
using Xunit;

namespace MiHomeUnitTests.Robustness;

public sealed class NetworkPacketFuzzTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    [Fact(Timeout = 30_000)]
    public async Task Miio_transport_skips_short_random_datagrams_before_valid_responses()
    {
        await using var emulator = new MiioEmulator(Token);
        using var transport = new MiioTransport("127.0.0.1", Token, emulator.Port)
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(100),
            ResponseTimeout = TimeSpan.FromSeconds(5),
        };
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var garbage = new byte[random.Next(1, 33)];
            random.NextBytes(garbage);
            emulator.SendBeforeNextResponse(garbage);
            try
            {
                var response = await transport.RequestAsync("get_prop", new[] { "power" }, MiioRetryPolicy.SafeToRepeat);
                Assert.Contains("\"result\"", response);
            }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}, miIO: {exception}");
            }
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task Mqtt_transport_reconnects_after_invalid_and_oversized_packet_lengths()
    {
        await using var broker = new MqttBrokerEmulator();
        using var transport = new MqttTransport("127.0.0.1", broker.Port, ["zigbee/send"], "zigbee/recv",
            NullLoggerFactory.Instance, operationTimeout: TimeSpan.FromSeconds(2),
            pingInterval: TimeSpan.FromSeconds(5), pingResponseTimeout: TimeSpan.FromSeconds(2), maxPacketBytes: 64)
        {
            ReconnectDelays = [TimeSpan.FromMilliseconds(20)],
        };

        var connectionEvents = Channel.CreateUnbounded<bool>();
        transport.ConnectionLost += () => connectionEvents.Writer.TryWrite(false);
        transport.ConnectionRestored += () => connectionEvents.Writer.TryWrite(true);
        var packetCases = new[]
        {
            new byte[] { 0x30, 0x80, 0x80, 0x80, 0x80, 0x00 },
            new byte[] { 0x30, 0x41 },
        };

        foreach (var packet in packetCases)
        {
            await broker.SendRawAsync(packet);
            Assert.False(await connectionEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await connectionEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            await Gateway2IntegrationTests.WaitUntilAsync(() => broker.ConnectedClientCount > 0, TimeSpan.FromSeconds(5));
        }

        var delivered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.OnMessageReceived += (_, payload) => delivered.TrySetResult(payload);
        await broker.PublishAsync("zigbee/send", "{\"cmd\":\"report\",\"did\":\"fixture\"}");
        Assert.Equal("{\"cmd\":\"report\",\"did\":\"fixture\"}",
            await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
