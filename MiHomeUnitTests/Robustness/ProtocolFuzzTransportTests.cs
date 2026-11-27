using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.MiioDevices;
using MiHomeLib.MqttGateway;
using MiHomeLib.MqttGateway.Discovery;
using MiHomeLib.XiaomiGateway2;
using MiHomeUnitTests.Integration;
using Xunit;

namespace MiHomeUnitTests.Robustness;

public sealed class ProtocolFuzzTransportTests
{
    [Fact]
    public async Task Gateway2_mutated_messages_do_not_stop_processing_a_later_valid_report()
    {
        const string sid = "plug-sid";
        var transport = new FakeGateway2Transport(sid);
        await using var gateway = new XiaomiGateway2(new FakeMiioTransport(), transport,
            "gateway-sid", 0, NullLoggerFactory.Instance);
        await gateway.DiscoverDevicesAsync();

        var messages = LoadFixtureMessages("gateway2.jsonl");
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            transport.Raise(SeededMutator.Mutate(messages[random.Next(messages.Length)], random));
            if (iteration % 100 == 99) await Task.Delay(5);
        }

        transport.Raise("{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"plug-sid\",\"data\":\"{\\\"status\\\":\\\"on\\\"}\"}");
        await Gateway2IntegrationTests.WaitUntilAsync(
            () => gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>(sid)?.State == XiaomiMiSmartPowerPlugCN.PlugState.On,
            TimeSpan.FromSeconds(5));
        Assert.Equal(XiaomiMiSmartPowerPlugCN.PlugState.On,
            gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>(sid)!.State);
    }

    [Fact]
    public async Task Mqtt_mutated_messages_do_not_stop_processing_a_later_valid_report()
    {
        const string did = "lumi.158d000000001234";
        var mqttTransport = new FakeMqttTransport();
        await using var gateway = new MultimodeGateway(did, new FakeMiioTransport(), mqttTransport,
            new FixedDevicesDiscoverer([new ZigBeeDeviceInfo(did, "lumi.plug")]), NullLoggerFactory.Instance);
        await gateway.DiscoverDevicesAsync();
        var plug = gateway.GetDeviceById<XiaomiMiSmartPowerPlugCN>(did);
        Assert.NotNull(plug);

        var messages = LoadFixtureMessages("mqtt.jsonl");
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            mqttTransport.Raise("zigbee/send", SeededMutator.Mutate(messages[random.Next(messages.Length)], random));
            if (iteration % 100 == 99) await Task.Delay(5);
        }

        mqttTransport.Raise("zigbee/send",
            $"{{\"cmd\":\"report\",\"did\":\"{did}\",\"time\":1794564562637,\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}");
        Assert.Equal(XiaomiMiSmartPowerPlugCN.PlugState.On, plug!.State);
    }

    private static string[] LoadFixtureMessages(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recorded", fileName);
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                return document.RootElement.GetProperty("message").GetString();
            })
            .ToArray();
    }

    private sealed class FakeGateway2Transport(string deviceSid) : IGateway2Transport
    {
        public string Token { get; set; }
        public event Action<string> OnMessageReceived;

        public void SendDiscover() => Raise(
            $"{{\"cmd\":\"get_id_list_ack\",\"sid\":\"gateway-sid\",\"token\":\"0123456789abcdef\",\"data\":\"[\\\"{deviceSid}\\\"]\"}}");

        public void SendRead(string sid)
        {
            if (sid == "gateway-sid")
                Raise("{\"cmd\":\"read_ack\",\"model\":\"gateway\",\"sid\":\"gateway-sid\",\"token\":\"0123456789abcdef\",\"data\":\"{}\"}");
            else if (sid == deviceSid)
                Raise($"{{\"cmd\":\"read_ack\",\"model\":\"plug\",\"sid\":\"{deviceSid}\",\"data\":\"{{\\\"status\\\":\\\"off\\\"}}\"}}");
        }

        public void SendWrite(string sid, string model, string gatewayPassword, params (string Key, string Value)[] data) { }
        public void Raise(string message) => OnMessageReceived?.Invoke(message);
        public void Dispose() { }
    }

    private sealed class FakeMqttTransport : IMqttTransport
    {
        public event Action<string, string> OnMessageReceived;
        public void SendMessage(string message) { }
        public void Publish(string topic, string message) { }
        public void Raise(string topic, string message) => OnMessageReceived?.Invoke(topic, message);
        public void Dispose() { }
    }

    private sealed class FakeMiioTransport : IMiioTransport
    {
        public string Ip => "127.0.0.1";
        public string Token => "0123456789abcdef0123456789abcdef";

        public string SendMessage(string message) => SendMessageAsync(message).GetAwaiter().GetResult();

        public Task<string> SendMessageAsync(string message)
        {
            var request = JsonNode.Parse(message)!.AsObject();
            var id = request["id"]?.DeepClone();
            if (request["method"]?.GetValue<string>() == "miIO.info")
                return Task.FromResult(new JsonObject
                {
                    ["id"] = id,
                    ["result"] = new JsonObject
                    {
                        ["life"] = 1,
                        ["miio_ver"] = "0.0.0",
                        ["fw_ver"] = "1.0.0",
                        ["mac"] = "00:00:00:00:00:00",
                        ["hw_ver"] = "emulator",
                        ["ap"] = new JsonObject
                        {
                            ["ssid"] = "loopback", ["bssid"] = "00:00:00:00:00:00", ["rssi"] = 0,
                        },
                        ["netif"] = new JsonObject
                        {
                            ["localIp"] = "127.0.0.1", ["mask"] = "255.0.0.0", ["gw"] = "127.0.0.1",
                        },
                    },
                }.ToJsonString());

            var parameters = request["params"] as JsonArray ?? [];
            var result = new JsonArray();
            for (var index = 1; index < parameters.Count; index++) result.Add(0);
            return Task.FromResult(new JsonObject { ["id"] = id, ["code"] = 0, ["result"] = result }.ToJsonString());
        }

        public string SendMessageRepeated(string message, int times = 3) => SendMessage(message);
        public void Dispose() { }
    }
}
