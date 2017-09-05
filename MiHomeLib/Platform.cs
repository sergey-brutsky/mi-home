using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Newtonsoft.Json;

namespace MiHomeLib
{
    public class Platform: IDisposable
    {
        private readonly UdpTransport _transport;
        private readonly List<MiHomeDevice> _devicesToWatch = new List<MiHomeDevice>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public Platform(UdpTransport transport)
        {
            _transport = transport;
        }

        public void Connect()
        {
            Task.Run(() => StartReceivingMessages(_cts.Token), _cts.Token);

            // Initialize devices
            foreach (var device in _devicesToWatch)
            {
                _transport.SendReadCommand(device.Sid, new ReadCommand(device).ToString());
            }
        }

        private async Task StartReceivingMessages(CancellationToken ct)
        {
            // Receive messages
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var str = await _transport.ReceiveAsync().ConfigureAwait(false);

                    Console.WriteLine(str);

                    var command = JsonConvert.DeserializeObject<ResponseCommand>(str);

                    if (command.Cmd == "heartbeat")
                    {
                        _transport.SetToken(command.Token);
                        continue;
                    }

                    var device = _devicesToWatch.FirstOrDefault(
                            x => x.Sid == command.Sid 
                        &&  command.Data != null 
                        &&  (command.Cmd == "read_ack" || command.Cmd == "report" || command.Cmd == "heartbeat")
                    );

                    device?.ParseData(command.Data);
                }
                catch (Exception)
                {
                    /* Nothing special, just blocking call was aborted */
                }
            }
        }
        
        public void Disconnect()
        {
            _cts?.Cancel();
            _transport?.Dispose();
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