using System.IO;
using System.Security.Cryptography;

namespace MiHomeLib.Helpers;

internal static class AesEncryption
{
    public static byte[] EncryptData(byte[] iv, byte[] key, byte[] bytes)
    {
        using var aesCbc128 = CreateAesCbc128(iv, key);

        return PerformTransformation(bytes, aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV));
    }

    public static byte[] DecryptData(byte[] iv, byte[] key, byte[] bytes)
    {
        using var aesCbc128 = CreateAesCbc128(iv, key);

        return PerformTransformation(bytes, aesCbc128.CreateDecryptor(aesCbc128.Key, aesCbc128.IV));
    }

    internal static Aes CreateAesCbc128(byte[] iv, byte[] key, PaddingMode mode = PaddingMode.PKCS7)
    {
        var aesCbc128 = Aes.Create();
        try
        {
            aesCbc128.KeySize = 128;
            aesCbc128.BlockSize = 128;
            aesCbc128.IV = iv;
            aesCbc128.Key = key;
            aesCbc128.Mode = CipherMode.CBC;
            aesCbc128.Padding = mode;

            return aesCbc128;
        }
        catch
        {
            aesCbc128.Dispose();
            throw;
        }
    }

    internal static byte[] PerformTransformation(byte[] data, ICryptoTransform transform)
    {
        using var ms = new MemoryStream();

        using (var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write))
        {
            cryptoStream.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }
}
