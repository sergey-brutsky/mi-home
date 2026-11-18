using System;
using System.Text.Json;

namespace MiHomeLib.MiioDevices.Internal;

internal static class MiioResponseReader
{
    public static JsonDocument Parse(string response, string operation)
    {
        if (response is null) throw new InvalidOperationException($"{operation}: null response.");
        JsonDocument document;
        try { document = JsonDocument.Parse(response); }
        catch (JsonException exception) { throw new InvalidOperationException($"{operation}: invalid JSON response.", exception); }
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && (!root.TryGetProperty("error", out var responseError) || responseError.ValueKind == JsonValueKind.Null) && root.TryGetProperty("result", out _))
            return document;
        var reason = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error)
            ? error.GetRawText() : "missing result";
        document.Dispose();
        throw new InvalidOperationException($"{operation}: {reason}.");
    }

    public static JsonElement Object(JsonElement value, string operation)
    {
        if (value.ValueKind == JsonValueKind.Object) return value;
        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var nested = JsonDocument.Parse(value.GetString());
                if (nested.RootElement.ValueKind == JsonValueKind.Object) return nested.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"{operation}: invalid JSON object.", exception);
            }
        }
        throw new InvalidOperationException($"{operation}: expected an object.");
    }

    public static JsonElement Property(JsonElement parent, string name, string operation)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
            throw new InvalidOperationException($"{operation}: missing field.");
        return value;
    }

    public static string StringProperty(JsonElement parent, string name, string operation)
    {
        var value = Property(parent, name, operation);
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"{operation}: expected a string.");
        return value.GetString();
    }

    public static bool OnOff(string value) => value switch
    {
        "on" => true,
        "off" => false,
        _ => throw new FormatException("Expected 'on' or 'off'.")
    };

    public static T ConvertValue<T>(Func<T> parse, string operation)
    {
        try { return parse(); }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new InvalidOperationException($"{operation}: invalid value.", exception);
        }
    }

    public static JsonElement First(JsonElement result, string operation)
    {
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            throw new InvalidOperationException($"{operation}: expected a non-empty result array.");
        return result[0];
    }

    public static void CheckStatus(JsonElement result, string operation)
    {
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("code", out var code) ||
            code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var status))
            throw new InvalidOperationException($"{operation}: missing or invalid result code.");
        if (status != 0) throw new InvalidOperationException($"{operation}: gateway returned code {status}.");
    }
}
