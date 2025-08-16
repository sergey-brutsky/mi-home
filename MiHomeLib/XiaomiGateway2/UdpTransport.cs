using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Threading;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.Transport;

namespace MiHomeLib.XiaomiGateway2;

internal class UdpTransport: IMessageTransport
{
    private readonly string _gwPassword;
    private readonly string _multicastAddress;
    private readonly int _serverPort;
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts = new(); 
    public string Token { get; set; }

    // TODO: Refactor me ! do not force to use gwPassword we can extract it automatically        
    public UdpTransport(string gwPassword, string multicastAddress = "224.0.0.50", int serverPort = 9898)
    {
        _gwPassword = gwPassword;
        _multicastAddress = multicastAddress;
        _serverPort = serverPort;

        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _serverPort));
        _udpClient.JoinMulticastGroup(IPAddress.Parse(_multicastAddress));

        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                var data = (await _udpClient.ReceiveAsync()).Buffer;
                OnMessageReceived?.Invoke(Encoding.ASCII.GetString(data));
            }
        }, _cts.Token);
    }

    public int SendCommand(Command command)
    {
        var buffer = Encoding.ASCII.GetBytes(command.ToString());

        return _udpClient.Send(buffer, buffer.Length, new IPEndPoint(IPAddress.Parse(_multicastAddress), _serverPort));
    }

    public int SendWriteCommand(string sid, string type, Command data)
    {
        var key = Helpers.BuildKey(Token, _gwPassword).ToHex();
        Console.WriteLine($"Sid: {sid}, Token: {Token}, Crypto key: {key}, data: {data}");

        return SendCommand(new WriteCommand(sid, type, key, data));
    }

    public event Action<string> OnMessageReceived;

    public void Dispose()
    {
        _udpClient?.Dispose();
        _cts?.Cancel();
    }
}