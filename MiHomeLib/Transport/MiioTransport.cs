using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("MiHomeUnitTests")]

namespace MiHomeLib.Devices
{
    public class MiioTransport : IMiioTransport
    {
        private readonly string _ip;
        private readonly string _token;
        private readonly Socket _socket;
        private readonly IPEndPoint _endpoint;
        
        private const int MESSAGES_TIMEOUT = 5000; // 5 seconds receive timeout
        private const string HELLO_REQUEST = "21310020ffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

        private readonly Exception _helloException = new Exception("Reponse hello package is corrupted, looks like miio protocol implemenation is broken");

        private readonly Exception _timeoutException = new Exception($"Response has not been received in {MESSAGES_TIMEOUT / 1000} seconds." +
                    $"Looks like miio protocol implementation is broken");

        private MiioPacket initialPacket = null;

        public string Ip => _ip;
        public string Token => _token;

        public MiioTransport(string ip, string token, int port = 54321)
        {
            _ip = ip ?? throw new Exception("IP of device must be provided");
            _token = token ?? throw new Exception("Token for device communication must be provided");

            _endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, MESSAGES_TIMEOUT);
        }

        public string SendMessage(string msg)
        {
            try
            {
                SendHelloPacketIfNeeded();

                var requestHex = initialPacket.BuildMessage(msg, _token);
                _socket.SendTo(requestHex.ToByteArray(), _endpoint);

                var responseHex = _socket.ReceiveBytes().ToHex();
                var miioPacket = new MiioPacket(responseHex);

                return miioPacket.GetResponseData(_token);
            }
            catch (TimeoutException)
            {
                throw _timeoutException;
            }
        }

        private void SendHelloPacketIfNeeded()
        {
            if (initialPacket == null)
            {
                _socket.SendTo(HELLO_REQUEST.ToByteArray(), _endpoint);

                var receviedHello = _socket.ReceiveBytes(32);

                if (receviedHello.Length != 32) // hello response message must be 32 bytes
                {
                    throw _helloException;
                }

                initialPacket = new MiioPacket(receviedHello.ToHex());
            }
        }

        public async Task<string> SendMessageAsync(string msg)
        {
            try
            {
                await SendHelloPacketIfNeededAsync().ConfigureAwait(false);

                var requestHex = initialPacket.BuildMessage(msg, _token);
                await _socket.SendToAsync(requestHex.ToArraySegment(), SocketFlags.None, _endpoint).ConfigureAwait(false);

                var responseHex = (await _socket.ReceiveBytesAsync().ConfigureAwait(false)).ToHex();
                var miioPacket = new MiioPacket(responseHex);

                return miioPacket.GetResponseData(_token);
            }
            catch (TimeoutException)
            {
                throw _timeoutException;
            }
        }

        private async Task SendHelloPacketIfNeededAsync()
        {
            if (initialPacket == null)
            {
                await _socket.SendToAsync(HELLO_REQUEST.ToArraySegment(), SocketFlags.None, _endpoint).ConfigureAwait(false);

                var receviedHello = await _socket.ReceiveBytesAsync(32).ConfigureAwait(false);

                if (receviedHello.Length != 32) // hello response message must be 32 bytes
                {
                    throw _helloException;
                }

                initialPacket = new MiioPacket(receviedHello.ToHex());
            }
        }

        public static List<(string ip, string type, string serial, string token)> SendDiscoverMessage(int port = 54321)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, MESSAGES_TIMEOUT);
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

        public void Dispose()
        {
            _socket?.Close();
        }
    }
}
