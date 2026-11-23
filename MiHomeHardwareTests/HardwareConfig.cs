namespace MiHomeHardwareTests;

internal sealed class HardwareConfig
{
    internal string? Gateway2Ip { get; } = Read("MIHOME_GW2_IP");
    internal string? Gateway2Token { get; } = Read("MIHOME_GW2_TOKEN");
    internal string? MqttKind { get; } = Read("MIHOME_MQTT_KIND");
    internal string? MqttIp { get; } = Read("MIHOME_MQTT_IP");
    internal string? MqttToken { get; } = Read("MIHOME_MQTT_TOKEN");
    internal string? MqttDid { get; } = Read("MIHOME_MQTT_DID");
    internal bool AllowWrites { get; } = Read("MIHOME_ALLOW_WRITES") == "1";
    internal bool Interactive { get; } = Read("MIHOME_INTERACTIVE") == "1";
    internal string? RecordFolder { get; } = Read("MIHOME_RECORD");
    internal HashSet<string> Writable { get; } = new((Read("MIHOME_WRITABLE") ?? "")
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
    internal bool HasGateway2 => Gateway2Ip is not null && Gateway2Token is not null;
    internal bool HasMqtt => MqttKind is not null && MqttIp is not null && MqttToken is not null;
    private static string? Read(string name) => Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
