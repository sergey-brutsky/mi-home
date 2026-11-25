using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeEmulator;

public sealed record RecordedMessage(string Gateway, string? Topic, string Message, string? Model);

public sealed class RecordedScenario
{
    private readonly IReadOnlyList<RecordedMessage> _messages;

    private RecordedScenario(IReadOnlyList<RecordedMessage> messages) => _messages = messages;

    public IReadOnlyList<RecordedMessage> Messages => _messages;

    public static RecordedScenario LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var messages = new List<RecordedMessage>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var item = JsonNode.Parse(line)?.AsObject() ??
                throw new JsonException($"Recorded scenario contains a non-object line in '{path}'.");
            messages.Add(new RecordedMessage(
                item["gateway"]?.GetValue<string>() ??
                    throw new JsonException($"Recorded scenario line in '{path}' is missing 'gateway'."),
                item["topic"]?.GetValue<string>(),
                item["message"]?.GetValue<string>() ??
                    throw new JsonException($"Recorded scenario line in '{path}' is missing 'message'."),
                item["model"]?.GetValue<string>()));
        }
        return new RecordedScenario(messages);
    }

    public static RecordedScenario LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        return new RecordedScenario(Directory.GetFiles(directory, "*.jsonl")
            .OrderBy(file => file, StringComparer.Ordinal)
            .SelectMany(file => LoadFile(file).Messages)
            .ToArray());
    }

    public async Task PlayAsync(Gateway2Emulator? gateway2 = null, MqttBrokerEmulator? mqtt = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var message in _messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(message.Gateway, "XiaomiGateway2", StringComparison.Ordinal))
            {
                if (gateway2 is null)
                    throw new InvalidOperationException("The scenario contains Gateway 2 messages but no Gateway2Emulator was supplied.");
                await gateway2.SendRawAsync(message.Message).ConfigureAwait(false);
            }
            else
            {
                if (mqtt is null)
                    throw new InvalidOperationException($"The scenario contains MQTT messages for '{message.Gateway}' but no MqttBrokerEmulator was supplied.");
                if (message.Topic is null)
                    throw new InvalidOperationException($"The scenario message for '{message.Gateway}' is missing its MQTT topic.");
                await mqtt.PublishAsync(message.Topic, message.Message).ConfigureAwait(false);
            }
        }
    }
}
