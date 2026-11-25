using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeBenchmarks;

[MemoryDiagnoser]
public class Gateway2MessageBenchmarks
{
    private static readonly string[] FixtureMessages = LoadMessages();
    private Gateway2Message _message;
    private GatewaySubDeviceBase _device;

    [Params(0, 1, 2, 3, 4, 5)]
    public int MessageIndex { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _message = Gateway2Message.Parse(FixtureMessages[MessageIndex]);
        if (!ProtocolDeviceRegistry.TryCreate(DeviceProtocol.XiaomiGateway2, _message.Model, _message.Sid, _message.ShortId,
            NullLoggerFactory.Instance, out _device))
            throw new InvalidOperationException($"Unsupported recorded model '{_message.Model}'.");
    }

    [Benchmark]
    public void ParseMessageAndApplyDeviceData()
    {
        var message = Gateway2Message.Parse(FixtureMessages[MessageIndex]);
        _device.ParseData(message.Data);
    }

    private static string[] LoadMessages() =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recorded", "gateway2.jsonl"))
            .Select(line =>
            {
                using var document = System.Text.Json.JsonDocument.Parse(line);
                return document.RootElement.GetProperty("message").GetString();
            })
            .ToArray();
}
