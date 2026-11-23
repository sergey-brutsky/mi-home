using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MiHomeHardwareTests;

public sealed class RecordingLoggerTests
{
    [SkippableFact]
    public void Gateway2_Recording_Removes_Tokens_And_Uses_Structured_State()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mihome-recording-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new RecordingLoggerProvider(folder, "private-test-token");
            var logger = provider.CreateLogger("MiHomeLib.XiaomiGateway2.XiaomiGateway2");
            logger.LogDebug("{Message}", "{\"cmd\":\"heartbeat\",\"token\":\"private-test-token\",\"nested\":{\"token\":\"other\"},\"value\":\"private-test-token\"}");
            var line = File.ReadAllText(Path.Combine(folder, "gateway2.jsonl"));
            line.Should().NotContain("private-test-token").And.NotContain("other");
            using var entry = JsonDocument.Parse(line);
            entry.RootElement.GetProperty("topic").ValueKind.Should().Be(JsonValueKind.Null);
            using var message = JsonDocument.Parse(entry.RootElement.GetProperty("message").GetString()!);
            message.RootElement.TryGetProperty("token", out _).Should().BeFalse();
            message.RootElement.GetProperty("nested").TryGetProperty("token", out _).Should().BeFalse();
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [SkippableFact]
    public void Mqtt_Recording_Preserves_Topic_And_Ignores_Unrelated_Logs_And_Invalid_Json()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mihome-recording-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new RecordingLoggerProvider(folder);
            var logger = provider.CreateLogger("MiHomeLib.MqttGateway.MultimodeGateway");
            logger.LogDebug("{Topic} --> {Message}", "zigbee/send", "{\"cmd\":\"report\"}");
            logger.LogDebug("{Topic} --> {Message}", "zigbee/send", "invalid secret text");
            logger.LogDebug("Other log {Message}", "{\"unexpected\":true}");
            provider.CreateLogger("Unrelated").LogDebug("{Topic} --> {Message}", "zigbee/send", "{}");
            var lines = File.ReadAllLines(Path.Combine(folder, "mqtt.jsonl"));
            lines.Should().HaveCount(1);
            using var entry = JsonDocument.Parse(lines[0]);
            entry.RootElement.GetProperty("topic").GetString().Should().Be("zigbee/send");
            entry.RootElement.GetProperty("gateway").GetString().Should().Be("MultimodeGateway");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
