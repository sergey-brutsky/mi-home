using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;

namespace MiHomeLib
{
    public class UdpTransport: IMessageTransport
    {
        private readonly string _gwPassword;
        private readonly string _multicastAddress;
        private readonly int _serverPort;
        private readonly Socket _socket;
        
        public string Token { get; set; }

        public UdpTransport(string gwPassword, string multicastAddress = "224.0.0.50", int serverPort = 9898)
        {
            _gwPassword = gwPassword;
            _multicastAddress = multicastAddress;
            _serverPort = serverPort;
        
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);            
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(_multicastAddress)));
            _socket.Bind(new IPEndPoint(IPAddress.Any, _serverPort));
        }

        public int SendCommand(Command command)
        {
            var buffer = Encoding.ASCII.GetBytes(command.ToString());

            return _socket.SendTo(buffer, new IPEndPoint(IPAddress.Parse(_multicastAddress), _serverPort));
        }

        public int SendWriteCommand(string sid, string type, Command data)
        {
            var key = CryptoProvider.BuildKey(Token, _gwPassword).ToHex();

            return SendCommand(new WriteCommand(sid, type, key, data));
        }

        public async Task<string> ReceiveAsync()
        {
            var data = await _socket.ReceiveBytesAsync(1024).ConfigureAwait(false);

            return Encoding.ASCII.GetString(data);
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }
    }
}