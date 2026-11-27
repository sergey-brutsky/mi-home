using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.MiioDevices;
using MiHomeLib.MqttGateway;
using MiHomeLib.MqttGateway.Discovery;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeBenchmarks;

[MemoryDiagnoser]
public class MqttMessageBenchmarks
{
    private static readonly RecordedMqttMessage[] FixtureMessages = LoadMessages();
    private FakeMqttTransport _transport;
    private MultimodeGateway _gateway;

    [Params(0, 1)]
    public int MessageIndex { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var devices = FixtureMessages.Select(message => new ZigBeeDeviceInfo(message.Did, message.Model)).ToList();
        _transport = new FakeMqttTransport();
        _gateway = new MultimodeGateway("gateway", new FakeMiioTransport(), _transport,
            new FixedDiscoverer(devices), NullLoggerFactory.Instance);
        _gateway.DiscoverDevices();
    }

    [Benchmark]
    public void ProcessRecordedMessage()
    {
        var message = FixtureMessages[MessageIndex];
        _transport.Raise(message.Topic, message.Message);
    }

    [GlobalCleanup]
    public void Cleanup() => _gateway.Dispose();

    private static RecordedMqttMessage[] LoadMessages() =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recorded", "mqtt.jsonl"))
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var message = root.GetProperty("message").GetString();
                using var messageDocument = JsonDocument.Parse(message);
                return new RecordedMqttMessage(
                    root.GetProperty("topic").GetString(),
                    message,
                    root.GetProperty("model").GetString(),
                    messageDocument.RootElement.GetProperty("did").GetString());
            })
            .ToArray();

    private sealed record RecordedMqttMessage(string Topic, string Message, string Model, string Did);

    private sealed class FixedDiscoverer(List<ZigBeeDeviceInfo> devices) : IDevicesDiscoverer
    {
        public List<BleDeviceInfo> DiscoverBleDevices() => [];
        public List<MeshDeviceInfo> DiscoverMeshDevices() => [];
        public List<ZigBeeDeviceInfo> DiscoverZigBeeDevices() => devices;
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
            var response = new JsonObject { ["id"] = request["id"]?.DeepClone() };
            if (request["method"]?.GetValue<string>() == "miIO.info")
            {
                response["result"] = new JsonObject
                {
                    ["life"] = 1,
                    ["miio_ver"] = "0.0.0",
                    ["fw_ver"] = "1.0.0",
                    ["mac"] = "00:00:00:00:00:00",
                    ["hw_ver"] = "benchmark",
                    ["ap"] = new JsonObject { ["ssid"] = "benchmark", ["bssid"] = "00:00:00:00:00:00", ["rssi"] = 0 },
                    ["netif"] = new JsonObject { ["localIp"] = "127.0.0.1", ["mask"] = "255.0.0.0", ["gw"] = "127.0.0.1" },
                };
            }
            else
            {
                var parameters = request["params"] as JsonArray ?? [];
                var values = new JsonArray();
                for (var index = 1; index < parameters.Count; index++)
                {
                    var property = parameters[index]?.GetValue<string>();
                    values.Add(property switch
                    {
                        "channel_0" or "channel_1" => JsonValue.Create("off"),
                        "load_power" => JsonValue.Create(0f),
                        _ => JsonValue.Create(0),
                    });
                }
                response["code"] = 0;
                response["result"] = values;
            }
            return Task.FromResult(response.ToJsonString());
        }

        public string SendMessageRepeated(string message, int times = 3) => SendMessage(message);
        public void Dispose() { }
    }
}
