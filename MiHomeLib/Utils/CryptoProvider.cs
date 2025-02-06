using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MiHomeLib.Utils;

public class CryptoProvider
{
    public static byte[] EncryptData(byte[] iv, byte[] key, byte[] data)
    {
        Aes aesCbc128 = CreateAesCbc128(iv, key);

        return PerformaTransformation(data, aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV));
    }

    public static byte[] DecryptData(byte[] iv, byte[] key, byte[] data)
    {
        Aes aesCbc128 = CreateAesCbc128(iv, key);

        return PerformaTransformation(data, aesCbc128.CreateDecryptor(aesCbc128.Key, aesCbc128.IV));
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

    private static byte[] PerformaTransformation(byte[] data, ICryptoTransform transform)
    {
        using var ms = new MemoryStream();
        using (var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write))
        {
            cryptoStream.Write(data, 0, data.Length);
        }

        return ms.ToArray();
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
        var data = Encoding.UTF8.GetBytes(token);
        var aesCbc128 = CreateAesCbc128(iv, key, PaddingMode.None);
        
        return PerformaTransformation(data, aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV));
    }
}