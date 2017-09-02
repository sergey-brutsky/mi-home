using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Newtonsoft.Json;

namespace MiHomeLib
{
    public class Platform: IDisposable
    {
        private Socket _socket;

        private readonly string _password;
        private readonly string _multicastAddress;
        private readonly int _multicastPort;
        private readonly List<MiHomeDevice> _devicesToWatch = new List<MiHomeDevice>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public Platform(string password, string multicastAddress = "224.0.0.50", int multicastPort = 9898)
        {
            _password = password;
            _multicastAddress = multicastAddress;
            _multicastPort = multicastPort;
        }

        public void Connect()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(_multicastAddress)));
            _socket.Bind(new IPEndPoint(IPAddress.Any, _multicastPort));

            // Initialize devices
            foreach (var device in _devicesToWatch)
            {
                var buffer = Encoding.ASCII.GetBytes(new ReadCommand(device).ToString());
                _socket.SendTo(buffer, 0, buffer.Length, 0, new IPEndPoint(IPAddress.Parse(_multicastAddress), _multicastPort));
            }

            Task.Run(() => StartReceivingMessages(_cts.Token), _cts.Token);
        }

        private async Task StartReceivingMessages(CancellationToken ct)
        {
            // Receive messages
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var data = new byte[1024];
                    var len = await _socket.ReceiveAsync(data, SocketFlags.None).ConfigureAwait(false);
                    var str = Encoding.ASCII.GetString(data, 0, len);

                    //Console.WriteLine(str);
                    //TODO: Add log events 

                    var command = JsonConvert.DeserializeObject<ResponseCommand>(str);

                    var device = _devicesToWatch.FirstOrDefault(x => x.Sid == command.Sid && command.Data != null);

                    device?.ParseData(command.Data);
                }
                catch (Exception e)
                {
                    /* Nothing special, just blocking call was aborted */
                }
            }
        }
        
        public void Disconnect()
        {
            _cts?.Cancel();
            _socket?.Shutdown(SocketShutdown.Both);
            _socket?.Close();
        }

        public void AddDeviceToWatch(MiHomeDevice device)
        {
            _devicesToWatch.Add(device);
        }
        public void RemoveDeviceToWatch(MiHomeDevice device)
        {
            _devicesToWatch.RemoveAll(x => x.Sid == device.Sid);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}