using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MiHomeLib
{
    public class KeyBuilder : IKeyBuilder
    {
        private readonly byte[] _initialVector =
            {0x17, 0x99, 0x6d, 0x09, 0x3d, 0x28, 0xdd, 0xb3, 0xba, 0x69, 0x5a, 0x2e, 0x6f, 0x58, 0x56, 0x2e};

        private readonly string _gatewayPassword;

        public KeyBuilder(string gatewayPassword)
        {
            _gatewayPassword = gatewayPassword;
        }

        public string BuildKey(string token)
        {
            if(_gatewayPassword == null)
            {
                throw new Exception("You cannot send commands to gateway without password");
            }

            if (token == null)
            {
                throw new Exception("Gateway token is null, not possible to send commands to devices");
            }

            byte[] encrypted;

            using (var aesCbc128 = Aes.Create())
            {
                aesCbc128.KeySize = 128;
                aesCbc128.BlockSize = 128;
                aesCbc128.IV = _initialVector;
                aesCbc128.Key = Encoding.ASCII.GetBytes(_gatewayPassword); ;
                aesCbc128.Mode = CipherMode.CBC;
                aesCbc128.Padding = PaddingMode.None;

                var encryptor = aesCbc128.CreateEncryptor(aesCbc128.Key, aesCbc128.IV);

                using (var ms = new MemoryStream())
                {
                    using (var cryptoStream = new StreamWriter(new CryptoStream(ms, encryptor, CryptoStreamMode.Write)))
                    {
                        cryptoStream.Write(token);
                    }

                    encrypted = ms.ToArray();
                }
            }

            return BitConverter.ToString(encrypted).Replace("-", string.Empty);
        }
    }
}