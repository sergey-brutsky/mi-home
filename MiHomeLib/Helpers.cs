using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MiHomeLib;
public static class Helpers
{
    public static byte[] ToByteArray(this string hex)
    {
        return Enumerable
            .Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    public static string ToJson(this object data)
    {
        return JsonSerializer.Serialize(data);
    }

    public static string ToHex(this byte[] byteArray)
    {
        return BitConverter.ToString(byteArray).Replace("-", string.Empty).ToLower();
    }
    
    public static DateTime UnixSecondsToDateTime(this double unixTimeStamp)
    {
        return (unixTimeStamp * 1000).UnixMilliSecondsToDateTime(); 
    }

    public static DateTime UnixMilliSecondsToDateTime(this double unixTimeStamp)
    {
        DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }

    public static IEnumerable<int> EnumToIntegers<T>()
    {
        return ((T[])Enum.GetValues(typeof(T))).Select(x => Convert.ToInt32(x));
    }

    public static byte[] EncryptData(byte[] iv, byte[] key, byte[] bytes)
    {
        Aes aesCbc128 = CreateAesCbc128(iv, key);

        return PerformTransformation(bytes, aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV));
    }

    public static byte[] DecryptData(byte[] iv, byte[] key, byte[] bytes)
    {
        Aes aesCbc128 = CreateAesCbc128(iv, key);

        return PerformTransformation(bytes, aesCbc128.CreateDecryptor(aesCbc128.Key, aesCbc128.IV));
    }

    public static byte[] BuildKey(string token, string gwPassword)
    {
        if (token == null)
        {
            throw new Exception("Gateway token is null, not possible to send commands to devices");
        }

        if (gwPassword == null)
        {
            throw new Exception("You cannot send commands to gateway without password");
        }

        // Magic vector for xiaomi gateway
        var iv = new byte[] { 0x17, 0x99, 0x6d, 0x09, 0x3d, 0x28, 0xdd, 0xb3, 0xba, 0x69, 0x5a, 0x2e, 0x6f, 0x58, 0x56, 0x2e};
        var key = Encoding.ASCII.GetBytes(gwPassword);
        var bytes = Encoding.UTF8.GetBytes(token);
        var aesCbc128 = CreateAesCbc128(iv, key, PaddingMode.None);
        
        return PerformTransformation(bytes, aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV));
    }

    private static Aes CreateAesCbc128(byte[] iv, byte[] key, PaddingMode mode = PaddingMode.PKCS7)
    {
        var aesCbc128 = Aes.Create();
        aesCbc128.KeySize = 128;
        aesCbc128.BlockSize = 128;
        aesCbc128.IV = iv;
        aesCbc128.Key = key;
        aesCbc128.Mode = CipherMode.CBC;
        aesCbc128.Padding = mode;

        return aesCbc128;
    }

    private static byte[] PerformTransformation(byte[] data, ICryptoTransform transform)
    {
        using var ms = new MemoryStream();

        using (var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write))
        {
            cryptoStream.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }
}
