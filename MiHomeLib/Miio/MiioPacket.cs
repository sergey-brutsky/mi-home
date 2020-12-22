using System;
using System.Security.Cryptography;
using System.Text;

namespace MiHomeLib.Devices
{
    public class MiioPacket
    {
        private readonly string _magic;
        private readonly string _length;
        private readonly string _unknown1;
        private readonly string _deviceType;
        private readonly string _serial;
        private readonly string _time;
        private readonly string _checksum;
        private readonly string _data;

        public MiioPacket(string hex)
        {
            _magic = hex.Substring(0, 4);
            _length = hex.Substring(4, 5);
            _unknown1 = hex.Substring(8, 8);
            _deviceType = hex.Substring(16, 4);
            _serial = hex.Substring(20, 4);
            _time = hex.Substring(24, 8);
            _checksum = hex.Substring(32, 32);

            if (hex.Length > 64) _data = hex.Substring(64, hex.Length - 64);
        }

        public string BuildMessage(string msg, string token)
        {
            var key = Md5(token);
            var iv = Md5($"{key}{token}");

            var encryptedData = CryptoProvider.EncryptData(iv.ToByteArray(), key.ToByteArray(), Encoding.UTF8.GetBytes(msg)).ToHex();
            var dataLength = (encryptedData.Length/2+32).ToString("x").PadLeft(4, '0');
            var checksum = Md5($"{_magic}{dataLength}{_unknown1}{_deviceType}{_serial}{_time}{token}{encryptedData}");

            return $"{_magic}{dataLength}{_unknown1}{_deviceType}{_serial}{_time}{checksum}{encryptedData}";
        }

        public string GetResponseData(string token)
        {
            var key = Md5(token);
            var iv = Md5($"{key}{token}");
            var data = CryptoProvider.DecryptData(iv.ToByteArray(), key.ToByteArray(), _data.ToByteArray());

            return Encoding.UTF8.GetString(data);
        }

        public string GetDeviceType() => _deviceType;

        public string GetChecksum() => _checksum;

        public string GetSerial() => _serial;

        private string Md5(string data) => MD5.Create().ComputeHash(data.ToByteArray()).ToHex();
    }
}