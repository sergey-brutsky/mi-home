using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeHardwareTests;

internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly string _folder;
    private readonly string[] _secrets;
    private readonly object _gate = new();
    internal RecordingLoggerProvider(string folder, params string?[] secrets)
    {
        _folder = Path.GetFullPath(folder);
        _secrets = secrets.Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToArray();
        Directory.CreateDirectory(_folder);
    }
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);
    public void Dispose() { }
    private void Record(string category, IEnumerable<KeyValuePair<string, object?>> state)
    {
        var fields = state.ToDictionary(p => p.Key, p => p.Value);
        var format = fields.GetValueOrDefault("{OriginalFormat}") as string;
        var gateway2 = category == "MiHomeLib.XiaomiGateway2.XiaomiGateway2" && format == "{Message}";
        var mqtt = category is "MiHomeLib.MqttGateway.MultimodeGateway" or "MiHomeLib.MqttGateway.MultimodeGateway2China"
            or "MiHomeLib.MqttGateway.MultimodeGateway2Global" or "MiHomeLib.MqttGateway.AqaraHubE1China";
        if (!gateway2 && !(mqtt && format == "{Topic} --> {Message}")) return;
        if (fields.GetValueOrDefault("Message") is not string message) return;
        JsonNode? parsed;
        try { parsed = JsonNode.Parse(message); }
        catch (JsonException) { return; } // Never write unparsed input that might contain credentials.
        RedactTokens(parsed);
        var sanitized = parsed?.ToJsonString() ?? "null";
        foreach (var secret in _secrets) sanitized = sanitized.Replace(secret, "<redacted>", StringComparison.Ordinal);
        var entry = new JsonObject
        {
            ["gateway"] = category[(category.LastIndexOf('.') + 1)..],
            ["topic"] = gateway2 ? null : fields.GetValueOrDefault("Topic") as string,
            ["message"] = sanitized
        }.ToJsonString();
        lock (_gate) File.AppendAllText(Path.Combine(_folder, gateway2 ? "gateway2.jsonl" : "mqtt.jsonl"), entry + Environment.NewLine);
    }
    private static void RedactTokens(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(p => p.Key).ToArray())
            {
                if (key.Equals("token", StringComparison.OrdinalIgnoreCase)) obj.Remove(key);
                else RedactTokens(obj[key]);
            }
        }
        else if (node is JsonArray array) foreach (var item in array) RedactTokens(item);
    }
    private sealed class RecordingLogger(RecordingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level == LogLevel.Debug;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level) && state is IEnumerable<KeyValuePair<string, object?>> fields) owner.Record(category, fields);
        }
    }
}

internal sealed class HardwareLoggerFactory : ILoggerFactory
{
    private readonly List<ILoggerProvider> _providers = [];
    internal HardwareLoggerFactory(HardwareConfig config)
    {
        if (config.RecordFolder is not null) _providers.Add(new RecordingLoggerProvider(config.RecordFolder, config.Gateway2Token, config.MqttToken));
    }
    public void AddProvider(ILoggerProvider provider) => _providers.Add(provider);
    public ILogger CreateLogger(string categoryName) => new Logger(_providers.Select(p => p.CreateLogger(categoryName)).ToArray());
    public void Dispose() { foreach (var provider in _providers) provider.Dispose(); }
    private sealed class Logger(ILogger[] loggers) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => loggers.Any(logger => logger.IsEnabled(level));
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            foreach (var logger in loggers) if (logger.IsEnabled(level)) logger.Log(level, id, state, error, formatter);
        }
    }
}
