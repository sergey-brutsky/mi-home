using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MiHomeLib.Transport;

[assembly: InternalsVisibleTo("MiHomeUnitTests")]

namespace MiHomeLib.MiioDevices;

internal class MiioTransport : IMiioTransport
{
    private readonly string _ip;
    private readonly string _token;
    private readonly UdpClient _udpClient;
    private IPEndPoint _endpoint;
    private const int MESSAGES_TIMEOUT_MS = 5000;
    private const string HELLO_REQUEST = "21310020ffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

    private readonly Exception _helloException =
        new("Reponse hello package is corrupted, looks like miio protocol implemenation is broken");

    private readonly Exception _timeoutException =
        new($"Response has not been received in {MESSAGES_TIMEOUT_MS / 1000} seconds." +
                $"Looks like miio protocol implementation is broken");

    private MiioPacket initialPacket = null;
    public string Ip => _ip;
    public string Token => _token;

    public MiioTransport(string ip, string token, int port = 54321)
    {
        _ip = ip ?? throw new Exception("IP of device must be provided");
        _token = token ?? throw new Exception("Token for device communication must be provided");

        _endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _udpClient = new UdpClient();
        _udpClient.Client.ReceiveTimeout = MESSAGES_TIMEOUT_MS;
    }

    public static List<(string ip, string type, string serial, string token)> SendDiscoverMessage(int port = 54321)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, MESSAGES_TIMEOUT_MS);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        socket.SendTo(HELLO_REQUEST.ToByteArray(), new IPEndPoint(IPAddress.Broadcast, port));

        var discoveredDevices = new List<(string ip, string type, string serial, string token)>();

        try
        {
            var buffer = new byte[4096];
            var bytesRead = 0;
            EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            while ((bytesRead = socket.ReceiveFrom(buffer, ref endPoint)) > 0)
            {
                var ip = ((IPEndPoint)endPoint).Address.ToString();
                var packet = new MiioPacket(new ArraySegment<byte>(buffer, 0, bytesRead).ToArray().ToHex());
                discoveredDevices.Add((ip, packet.GetDeviceType(), packet.GetSerial(), packet.GetChecksum()));
            }
        }
        catch { } // Normal situation, no more data in socket

        socket.Close();

        return discoveredDevices;
    }

    public string SendMessage(string msg)
    {
        try
        {
            SendHelloPacketIfNeeded();

            var bytes = initialPacket.BuildMessage(msg, _token).ToByteArray();
            
            _udpClient.Send(bytes, bytes.Length, _endpoint);
            
            return new MiioPacket(_udpClient.Receive(ref _endpoint).ToHex()).GetResponseData(_token);
        }
        catch (TimeoutException)
        {
            throw _timeoutException;
        }
    }

    public async Task<string> SendMessageAsync(string msg)
    {
        try
        {
            await SendHelloPacketIfNeededAsync().ConfigureAwait(false);

            var bytes = initialPacket.BuildMessage(msg, _token).ToByteArray();

            await _udpClient.SendAsync(bytes, bytes.Length, _endpoint).ConfigureAwait(false);
            
            var responseHex = (await _udpClient.ReceiveAsync().ConfigureAwait(false)).Buffer.ToHex();
            
            return new MiioPacket(responseHex).GetResponseData(_token);
        }
        catch (TimeoutException)
        {
            throw _timeoutException;
        }
    }

    public string SendMessageRepeated(string msg, int times)
    {
        string result = string.Empty;
        Exception ex = null;

        for (int i = 0; i < times; i++)
        {
            bool exceptionRaised = false;
            try
            {
                result = SendMessage(msg);
            }
            catch (Exception e)
            {
                ex = e;
                exceptionRaised = true;
            }

            if (!exceptionRaised) return result;
        }

        throw ex;
    }

    public void Dispose() => _udpClient?.Close();
    
    private void SendHelloPacketIfNeeded()
    {
        if (initialPacket == null)
        {
            var bytes = HELLO_REQUEST.ToByteArray();

            _udpClient.Send(bytes, bytes.Length, _endpoint);

            var receviedHello = _udpClient.Receive(ref _endpoint);

            if (receviedHello.Length != 32) // hello response message must be 32 bytes
            {
                throw _helloException;
            }

            initialPacket = new MiioPacket(receviedHello.ToHex());
        }
    }
    
    private async Task SendHelloPacketIfNeededAsync()
    {
        if (initialPacket == null)
        {
            var bytes = HELLO_REQUEST.ToByteArray();
            await _udpClient.SendAsync(bytes, bytes.Length, _endpoint).ConfigureAwait(false);
            
            var receviedHello = (await _udpClient.ReceiveAsync().ConfigureAwait(false)).Buffer;

            if (receviedHello.Length != 32) // hello response message must have 32 bytes
            {
                throw _helloException;
            }

            initialPacket = new MiioPacket(receviedHello.ToHex());
        }
    }

    internal class MiioPacket
    {
        private readonly string _magic;
        private readonly string _unknown1;
        private readonly string _deviceType;
        private readonly string _serial;
        private readonly string _time;
        private readonly string _checksum;
        private readonly string _data;

        public MiioPacket(string hex)
        {
            _magic = hex.Substring(0, 4);
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

            var encryptedData = Helpers.EncryptData(iv.ToByteArray(), key.ToByteArray(), Encoding.UTF8.GetBytes(msg)).ToHex();
            var dataLength = (encryptedData.Length / 2 + 32).ToString("x").PadLeft(4, '0');
            var checksum = Md5($"{_magic}{dataLength}{_unknown1}{_deviceType}{_serial}{_time}{token}{encryptedData}");

            return $"{_magic}{dataLength}{_unknown1}{_deviceType}{_serial}{_time}{checksum}{encryptedData}";
        }

        public string GetResponseData(string token)
        {
            var key = Md5(token);
            var iv = Md5($"{key}{token}");
            var data = Helpers.DecryptData(iv.ToByteArray(), key.ToByteArray(), _data.ToByteArray());

            return Encoding.UTF8.GetString(data);
        }

        public string GetDeviceType() => _deviceType;

        public string GetChecksum() => _checksum;

        public string GetSerial() => _serial;

        private string Md5(string data) => MD5.Create().ComputeHash(data.ToByteArray()).ToHex();
    }
}
