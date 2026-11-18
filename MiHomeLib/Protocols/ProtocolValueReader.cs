using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeLib.Protocols;

/// <summary>Normalizes malformed wire values to FormatException without catching handler defects.</summary>
internal static class ProtocolValueReader
{
    public static int ReadInt32(this JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        throw new FormatException("Expected an Int32 JSON number.");
    }

    public static double ReadDouble(this JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) &&
            !double.IsNaN(number) && !double.IsInfinity(number)) return number;
        throw new FormatException("Expected a finite JSON number.");
    }

    public static byte ReadByte(this JsonElement value) => checked((byte)value.ReadInt32());

    public static float ReadSingle(this JsonElement value) => value.ReadDouble().ToFiniteSingle();

    public static float ReadSingle(this JsonNode value) => ((double)value.GetValue<float>()).ToFiniteSingle();

    public static float ToFiniteSingle(this double value)
    {
        var number = (float)value;
        if (!float.IsNaN(number) && !float.IsInfinity(number)) return number;
        throw new FormatException("Expected a finite number within the Single range.");
    }

    public static bool ReadBoolean(this JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new FormatException("Expected a JSON boolean.")
    };

    public static string ReadString(this JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        throw new FormatException("Expected a JSON string.");
    }
}
