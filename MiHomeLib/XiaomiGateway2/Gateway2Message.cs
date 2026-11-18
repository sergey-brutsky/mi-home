using System.Globalization;
using System.Text.Json;

namespace MiHomeLib.XiaomiGateway2;

internal enum Gateway2MessageType
{
    Unknown,
    GetIdListAck,
    ReadAck,
    Heartbeat,
    Report,
    WriteAck,
}

/// <summary>
/// A message Xiaomi Gateway 2 sends to the multicast group, e.g.
/// {"cmd":"report","model":"plug","sid":"158d...","short_id":11109,"data":"{\"status\":\"on\"}"}
/// </summary>
internal sealed class Gateway2Message
{
    public string Command { get; }
    public Gateway2MessageType Type { get; }
    public string Model { get; }
    public string Sid { get; }
    public int ShortId { get; }
    public string Token { get; }

    /// <summary>
    /// The "data" field. The gateway sends it as a JSON document inside a string
    /// </summary>
    public string Data { get; }

    private Gateway2Message(string command, Gateway2MessageType type, string model, string sid, int shortId, string token, string data)
    {
        Command = command;
        Type = type;
        Model = model;
        Sid = sid;
        ShortId = shortId;
        Token = token;
        Data = data;
    }

    public static Gateway2Message Parse(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            var command = RequiredString(root, "cmd");

            var type = command switch
            {
                "get_id_list_ack" => Gateway2MessageType.GetIdListAck,
                "read_ack" => Gateway2MessageType.ReadAck,
                "heartbeat" => Gateway2MessageType.Heartbeat,
                "report" => Gateway2MessageType.Report,
                "write_ack" => Gateway2MessageType.WriteAck,
                _ => Gateway2MessageType.Unknown,
            };

            if (type == Gateway2MessageType.Unknown)
                return new Gateway2Message(command, type, null, null, 0, null, null);

            return new Gateway2Message(
                command,
                type,
                type is Gateway2MessageType.GetIdListAck or Gateway2MessageType.WriteAck
                    ? GetString(root, "model") : RequiredString(root, "model"),
                RequiredString(root, "sid"),
                GetShortId(root),
                GetString(root, "token"),
                GetString(root, "data") ?? throw new JsonException("Missing data field."));
        }
        catch (System.Exception e) when (e is JsonException or System.InvalidOperationException or System.Collections.Generic.KeyNotFoundException)
        {
            throw new JsonException($"Message of Xiaomi Gateway 2 cannot be parsed: '{message}'", e);
        }
    }

    private static string RequiredString(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
            throw new JsonException($"Missing or invalid '{name}' field.");
        return value.GetString();
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? (value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText())
            : null;

    // The gateway sends short_id as a number, the gateway itself as a string
    private static int GetShortId(JsonElement root)
    {
        if (!root.TryGetProperty("short_id", out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : 0;
    }
}
