using System;

namespace MiHomeLib.Helpers;

internal static class HexExtensions
{
    public static byte[] ToByteArray(this string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        if (hex.Length % 2 != 0) throw new FormatException("Hex string must contain an even number of characters.");
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = hex.DecodeByteAt(i * 2);
        return bytes;
    }

    public static string ToHex(this byte[] byteArray)
    {
        if (byteArray is null) throw new ArgumentNullException(nameof(byteArray));
        const string digits = "0123456789abcdef";
        var characters = new char[checked(byteArray.Length * 2)];
        for (var i = 0; i < byteArray.Length; i++)
        {
            characters[i * 2] = digits[byteArray[i] >> 4];
            characters[i * 2 + 1] = digits[byteArray[i] & 15];
        }
        return new string(characters);
    }
    // The caller validates the input length before reading a pair of hex digits.
    internal static byte DecodeByteAt(this string hex, int offset) =>
        (byte)((DecodeDigit(hex[offset]) << 4) | DecodeDigit(hex[offset + 1]));

    private static int DecodeDigit(char value)
    {
        if (value >= '0' && value <= '9') return value - '0';
        if (value >= 'a' && value <= 'f') return value - 'a' + 10;
        if (value >= 'A' && value <= 'F') return value - 'A' + 10;
        throw new FormatException("Invalid hexadecimal digit.");
    }
}
