using MiHomeLib.Helpers;
using System;
using System.Text;
using System.Security.Cryptography;

namespace MiHomeLib.XiaomiGateway2;

internal static class Gateway2KeyBuilder
{
    public static byte[] BuildKey(string token, string gwPassword)
    {
        if (token == null)
        {
            throw new ArgumentNullException(nameof(token), "Gateway token is null, not possible to send commands to devices");
        }

        if (gwPassword == null)
        {
            throw new ArgumentNullException(nameof(gwPassword), "You cannot send commands to gateway without password");
        }

        // Magic vector for xiaomi gateway
        var iv = new byte[] { 0x17, 0x99, 0x6d, 0x09, 0x3d, 0x28, 0xdd, 0xb3, 0xba, 0x69, 0x5a, 0x2e, 0x6f, 0x58, 0x56, 0x2e};
        var key = Encoding.ASCII.GetBytes(gwPassword);
        var bytes = Encoding.UTF8.GetBytes(token);
        using var aesCbc128 = AesEncryption.CreateAesCbc128(iv, key, PaddingMode.None);

        return AesEncryption.PerformTransformation(bytes, aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV));
    }

}
