using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal static class MqttJson
{
    public static JsonElement Object(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object ? node : throw new FormatException("Expected a JSON object.");

    public static JsonElement Array(JsonElement node) =>
        node.ValueKind == JsonValueKind.Array ? node : throw new FormatException("Expected a JSON array.");

    public static JsonElement Optional(JsonElement node, string name) =>
        Object(node).TryGetProperty(name, out var value) ? value : default;

    public static bool HasValue(JsonElement node) =>
        node.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

    public static string String(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(node.GetString()))
            return node.GetString();
        throw new FormatException("Expected a non-empty string.");
    }

    public static double Number(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Number && node.TryGetDouble(out var result) && !double.IsNaN(result) && !double.IsInfinity(result))
            return result;
        throw new FormatException("Expected a finite number.");
    }

    public static bool TryRead<T>(Func<T> read, ILogger logger, string action, out T value)
    {
        try
        {
            value = read();
            return true;
        }
        // This boundary covers wire parsing only. Device handlers run after it returns.
        catch (Exception exception) when (exception is JsonException or FormatException or
            InvalidOperationException or KeyNotFoundException or OverflowException or ArgumentException)
        {
            logger.LogWarning("Invalid MQTT data for '{Action}'. Processing is skipped: {Reason}", action, exception.Message);
            value = default;
            return false;
        }
    }
}
