using MiHomeLib.Helpers;
using System;

namespace MiHomeLib.Protocols;

/// <summary>Decodes hex values carried by MiBeacon BLE reports.</summary>
internal static class BleValueDecoder
{
    public static uint DecodeUnsignedLittleEndian(string hex, int byteCount)
    {
        if (byteCount < 1 || byteCount > 4)
            throw new ArgumentOutOfRangeException(nameof(byteCount));

        return Decode(hex, byteCount, "an unsigned value");
    }

    public static byte DecodeByte(string hex, string valueDescription) =>
        (byte)Decode(hex, 1, valueDescription);

    /// <summary>Decodes a signed little-endian int16 with a scale of 0.1.</summary>
    public static float DecodeSignedTenths(string hex) =>
        unchecked((short)Decode(hex, 2, "a temperature or humidity value")) / 10f;

    private static uint Decode(string hex, int byteCount, string valueDescription)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        if (hex.Length % 2 != 0)
            throw new FormatException("Hex string must contain an even number of characters.");
        if (hex.Length / 2 != byteCount)
            throw new FormatException($"Expected {byteCount} bytes for {valueDescription}, received {hex.Length / 2}.");

        uint result = 0;
        for (var i = 0; i < byteCount; i++)
            result |= (uint)hex.DecodeByteAt(i * 2) << (8 * i);
        return result;
    }
}
