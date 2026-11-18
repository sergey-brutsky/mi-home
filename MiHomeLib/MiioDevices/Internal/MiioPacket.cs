using MiHomeLib.Helpers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MiHomeLib.MiioDevices.Internal;

/// <summary>
/// Key and IV of the miio encryption, derived from the device token once: key = MD5(token), iv = MD5(key + token)
/// </summary>
internal sealed class MiioCipher
{
    public MiioCipher(string token)
    {
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (token.Length != 32) throw new ArgumentException("Miio token must contain exactly 32 hexadecimal characters (16 bytes).", nameof(token));
        try { Token = token.ToByteArray(); }
        catch (FormatException exception)
        {
            throw new ArgumentException("Miio token must contain exactly 32 hexadecimal characters (16 bytes).", nameof(token), exception);
        }
        using var md5 = MD5.Create();
        Key = md5.ComputeHash(Token);
        Iv = md5.ComputeHash(Concat(Key, Token));
    }

    public byte[] Token { get; }
    public byte[] Key { get; }
    public byte[] Iv { get; }

    public byte[] Encrypt(byte[] data) => AesEncryption.EncryptData(Iv, Key, data);
    public byte[] Decrypt(byte[] data) => AesEncryption.DecryptData(Iv, Key, data);

    public static byte[] Concat(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }
}

internal partial class MiioTransport
{
    /// <summary>
    /// A miio packet: a 32 byte header (magic 0x2131, length, unknown, device type, serial, stamp, checksum)
    /// followed by the encrypted payload
    /// </summary>
    internal class MiioPacket
    {
        private const int HEADER_LENGTH = 32;
        private const int STAMP_OFFSET = 12;
        private const int CHECKSUM_OFFSET = 16;

        private readonly byte[] _header;
        private readonly byte[] _data;

        public MiioPacket(string hex) : this(ParseHex(hex)) { }

        public MiioPacket(byte[] bytes)
        {
            if (bytes is null || bytes.Length < HEADER_LENGTH || bytes[0] != 0x21 || bytes[1] != 0x31)
                throw new InvalidDataException("Miio packet header is missing or has invalid magic.");
            var declaredLength = (bytes[2] << 8) | bytes[3];
            if (declaredLength != bytes.Length)
                throw new InvalidDataException("Miio packet length does not match its header.");

            _header = new byte[HEADER_LENGTH];
            Buffer.BlockCopy(bytes, 0, _header, 0, HEADER_LENGTH);
            if (bytes.Length > HEADER_LENGTH)
            {
                _data = new byte[bytes.Length - HEADER_LENGTH];
                Buffer.BlockCopy(bytes, HEADER_LENGTH, _data, 0, _data.Length);
            }
        }

        /// <summary>
        /// Stamp of the device clock in seconds, from the header
        /// </summary>
        public uint Stamp => (uint)(_header[STAMP_OFFSET] << 24 | _header[STAMP_OFFSET + 1] << 16 | _header[STAMP_OFFSET + 2] << 8 | _header[STAMP_OFFSET + 3]);

        /// <summary>
        /// A request with the header of this packet, e.g. the hello answer, and its stamp
        /// </summary>
        public string BuildMessage(string msg, string token) => BuildMessage(msg, new MiioCipher(token), Stamp).ToHex();

        /// <summary>
        /// A request with the device type and serial of this packet and the given stamp of the device clock
        /// </summary>
        public byte[] BuildMessage(string msg, MiioCipher cipher, uint stamp)
        {
            var encrypted = cipher.Encrypt(Encoding.UTF8.GetBytes(msg));
            var packet = new byte[HEADER_LENGTH + encrypted.Length];
            Buffer.BlockCopy(_header, 0, packet, 0, CHECKSUM_OFFSET);
            packet[2] = (byte)(packet.Length >> 8);
            packet[3] = (byte)packet.Length;
            packet[STAMP_OFFSET] = (byte)(stamp >> 24);
            packet[STAMP_OFFSET + 1] = (byte)(stamp >> 16);
            packet[STAMP_OFFSET + 2] = (byte)(stamp >> 8);
            packet[STAMP_OFFSET + 3] = (byte)stamp;
            // The checksum is calculated with the token in its place
            Buffer.BlockCopy(cipher.Token, 0, packet, CHECKSUM_OFFSET, 16);
            Buffer.BlockCopy(encrypted, 0, packet, HEADER_LENGTH, encrypted.Length);
            var checksum = Md5(packet);
            Buffer.BlockCopy(checksum, 0, packet, CHECKSUM_OFFSET, 16);
            return packet;
        }

        public string GetResponseData(string token) => GetResponseData(new MiioCipher(token));

        public string GetResponseData(MiioCipher cipher)
        {
            if (_data is null)
                throw new InvalidDataException(
                    "The Miio response has no encrypted payload. Verify the gateway token and check that the gateway returned a valid response.");

            var withToken = new byte[HEADER_LENGTH + _data.Length];
            Buffer.BlockCopy(_header, 0, withToken, 0, CHECKSUM_OFFSET);
            Buffer.BlockCopy(cipher.Token, 0, withToken, CHECKSUM_OFFSET, 16);
            Buffer.BlockCopy(_data, 0, withToken, HEADER_LENGTH, _data.Length);
            if (!FixedEquals(Md5(withToken), _header, CHECKSUM_OFFSET))
                throw new InvalidDataException(
                    "The Miio response checksum is invalid. The gateway token may be incorrect; verify the token configured for this gateway.");

            return Encoding.UTF8.GetString(cipher.Decrypt(_data));
        }

        public string GetDeviceType() => _header.ToHex().Substring(16, 4);

        public string GetChecksum() => _header.ToHex().Substring(32, 32);

        public string GetSerial() => _header.ToHex().Substring(20, 4);

        private static byte[] ParseHex(string hex)
        {
            try { return hex.ToByteArray(); }
            catch (Exception exception) when (exception is FormatException or ArgumentNullException)
            {
                throw new InvalidDataException("Miio packet is not valid hex.", exception);
            }
        }

        private static byte[] Md5(byte[] data)
        {
            using var md5 = MD5.Create();
            return md5.ComputeHash(data);
        }

        private static bool FixedEquals(byte[] checksum, byte[] header, int offset)
        {
            var difference = 0;
            for (var i = 0; i < checksum.Length; i++) difference |= checksum[i] ^ header[offset + i];
            return difference == 0;
        }
    }
}
