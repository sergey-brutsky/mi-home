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

    [Fact(Timeout = 60_000)]
    public async Task Miio_transport_skips_long_and_corrupted_datagrams_before_valid_responses()
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
            emulator.SendBeforeNextResponse(CorruptedMiioPacket(random, iteration % 3));
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
    public async Task Miio_transport_reports_checksum_error_when_no_valid_response_arrives()
    {
        // A client with a wrong token sees every answer of the device with a broken checksum
        await using var emulator = new MiioEmulator(Token) { CorruptResponseChecksums = true };
        using var transport = new MiioTransport("127.0.0.1", Token, emulator.Port)
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(100),
            ResponseTimeout = TimeSpan.FromMilliseconds(500),
        };

        var exception = await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
            transport.RequestAsync("get_prop", new[] { "power" }, MiioRetryPolicy.SafeToRepeat));
        Assert.Contains("checksum", exception.Message);
    }

    // 0: random bytes longer than the header, 1: valid header and length with a random body (wrong checksum),
    // 2: valid magic with a length that does not match the datagram
    private static byte[] CorruptedMiioPacket(Random random, int kind)
    {
        var packet = new byte[random.Next(33, 200)];
        random.NextBytes(packet);
        if (kind == 0) return packet;

        packet[0] = 0x21;
        packet[1] = 0x31;
        var length = kind == 1 ? packet.Length : packet.Length + random.Next(1, 100);
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)length;
        return packet;
    }

    [Fact(Timeout = 120_000)]
    public async Task Mqtt_transport_survives_seeded_random_packets()
    {
        await using var broker = new MqttBrokerEmulator();
        using var transport = new MqttTransport("127.0.0.1", broker.Port, ["zigbee/send"], "zigbee/recv",
            NullLoggerFactory.Instance, operationTimeout: TimeSpan.FromSeconds(2),
            pingInterval: TimeSpan.FromSeconds(5), pingResponseTimeout: TimeSpan.FromSeconds(2), maxPacketBytes: 256)
        {
            ReconnectDelays = [TimeSpan.FromMilliseconds(20)],
        };
        var delivered = Channel.CreateUnbounded<string>();
        transport.OnMessageReceived += (_, payload) => delivered.Writer.TryWrite(payload);
        await Gateway2IntegrationTests.WaitUntilAsync(() => broker.ConnectedClientCount > 0, TimeSpan.FromSeconds(5));
        await PublishUntilDeliveredAsync(broker, delivered.Reader, "{\"ready\":true}", TimeSpan.FromSeconds(5));

        var random = new Random(SeededMutator.Seed);
        // Each iteration may cost a reconnect, so the network cases are a tenth of the parser iterations
        var iterations = Math.Max(1, SeededMutator.Iterations / 10);
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var garbage = new byte[random.Next(1, 64)];
            random.NextBytes(garbage);
            // Half of the cases look like a PUBLISH, so the length and topic parsing is reached as well
            if (random.Next(2) == 0) garbage[0] = 0x30;
            var payload = $"{{\"iteration\":{iteration}}}";
            try
            {
                await broker.SendRawAsync(garbage);
                // Garbage can leave the stream waiting for the rest of a declared packet; the broker then drops
                // the connection the way a real network failure would, and the transport has to reconnect.
                if (!await TryPublishUntilDeliveredAsync(broker, delivered.Reader, payload, TimeSpan.FromMilliseconds(300)))
                {
                    await broker.DropConnectionsAsync();
                    await PublishUntilDeliveredAsync(broker, delivered.Reader, payload, TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}, garbage {Convert.ToHexString(garbage)}: {exception}");
            }
        }
    }

    private static async Task PublishUntilDeliveredAsync(MqttBrokerEmulator broker, ChannelReader<string> delivered,
        string payload, TimeSpan timeout)
    {
        if (!await TryPublishUntilDeliveredAsync(broker, delivered, payload, timeout))
            throw new TimeoutException($"MQTT message {payload} was not delivered within {timeout}.");
    }

    private static async Task<bool> TryPublishUntilDeliveredAsync(MqttBrokerEmulator broker, ChannelReader<string> delivered,
        string payload, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            // A message published while the transport reconnects or resubscribes is lost, so it is repeated
            if (broker.ConnectedClientCount > 0)
            {
                try { await broker.PublishAsync("zigbee/send", payload); }
                catch (Exception) { /* the connection is being dropped */ }
            }
            using var wait = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            try
            {
                while (true)
                    if (await delivered.ReadAsync(wait.Token) == payload) return true;
            }
            catch (OperationCanceledException) { }
        }
        return false;
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
