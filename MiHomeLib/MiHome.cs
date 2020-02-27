using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using MiHomeLib.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiHomeLib
{
    public class MiHome : IDisposable
    {
        private Gateway _gateway;
        private readonly string _gatewaySid;
        private static IMessageTransport _transport;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ConcurrentDictionary<string, MiHomeDevice> _devicesList = new ConcurrentDictionary<string, MiHomeDevice>();
        private readonly Dictionary<string, string> _namesMap;

        private const int ReadDeviceInterval = 100;
        
        private readonly Dictionary<string, Func<string, MiHomeDevice>> _devicesMap = new Dictionary<string, Func<string, MiHomeDevice>>
        {
            {"gateway",          sid => new Gateway(sid, _transport)},
            {"sensor_ht",        sid => new ThSensor(sid)},
            {"weather.v1",       sid => new WeatherSensor(sid)},
            {"motion",           sid => new MotionSensor(sid)},
            {"plug",             sid => new SocketPlug(sid, _transport)},
            {"magnet",           sid => new DoorWindowSensor(sid)},
            {"sensor_wleak.aq1", sid => new WaterLeakSensor(sid)},
            {"smoke",            sid => new SmokeSensor(sid)},
            {"switch",           sid => new Switch(sid)},
            {"ctrl_neutral2",    sid => new WiredDualWallSwitch(sid)},
            {"remote.b286acn01", sid => new WirelessDualWallSwitch(sid)},
        };

        private readonly Dictionary<string, Action<ResponseCommand>> _commandsToActions;
        private readonly Task _receiveTask;
        private readonly ILogger<MiHome> _logger;

        public MiHome(Dictionary<string, string> namesMap, string gatewayPassword = null, string gatewaySid = null): this(gatewayPassword, gatewaySid)
        {
            if (namesMap.GroupBy(x => x.Value).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Values in the dictionary must be unique");
            }

            _namesMap = namesMap;
        }

        public MiHome(string gatewayPassword = null, string gatewaySid = null) : this(gatewayPassword, gatewaySid, null, null) {}
        
        public MiHome(IMessageTransport transport, ILogger<MiHome> logger) : this(null, null, transport, logger) {}

        private MiHome(string gatewayPassword = null, string gatewaySid = null, IMessageTransport transport = null, ILogger<MiHome> logger = null)
        {
            if (logger == null)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.ClearProviders();
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddConsole();
                });

                _logger = loggerFactory.CreateLogger<MiHome>();
            }
            else
            {
                _logger = logger;
            }

            _commandsToActions = new Dictionary<string, Action<ResponseCommand>>
            {
                { "get_id_list_ack", DiscoverGatewayAndDevices},
                { "read_ack", ProcessReadAck},
                { "heartbeat", ProcessHeartbeat},
                { "report", ProcessReport},
            };

            _gatewaySid = gatewaySid;

            _transport = transport ?? new UdpTransport(gatewayPassword);

            _receiveTask = Task.Run(() => StartReceivingMessages(_cts.Token), _cts.Token);

            _transport.SendCommand(new DiscoverGatewayCommand());
        }

        private async Task StartReceivingMessages(CancellationToken ct)
        {
            // Receive messages
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    //TODO: Remove asynchronious here (no need for that)
                    var str = await _transport.ReceiveAsync().ConfigureAwait(false);
                    //_logger.LogInformation(str);
                    var respCmd = ResponseCommand.FromString(str);

                    //TODO: Check that command type is known
                    //TODO: Check that command model is known
                    //TODO: Emit an approprite event Report, Hearbeat, ReadAck
                    //TODO: Create a MiHomeDevice object and call ParseData method here
                    
                    //if (!_commandsToActions.ContainsKey(respCmd.Cmd)) continue;

                    //_commandsToActions[respCmd.Cmd](respCmd);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);
                }
            }
        }

        public IReadOnlyCollection<MiHomeDevice> GetDevices()
        {
            return (IReadOnlyCollection<MiHomeDevice>) _devicesList.Values;
        }

        public Gateway GetGateway()
        {
            return _gateway;
        }

        public T GetDeviceByName<T>(string name) where T : MiHomeDevice
        {
            var device = _devicesList.Values.FirstOrDefault(x => x.Name == name);

            if (device == null) return null;

            if (device is T d) return d;

            throw new InvalidCastException($"Device with name '{name}' cannot be converted to {nameof(T)}");
        }

        public T GetDeviceBySid<T>(string sid) where T : MiHomeDevice
        {
            if (!_devicesList.ContainsKey(sid)) throw new ArgumentException($"There is no device with sid '{sid}'");

            if(!(_devicesList[sid] is T device)) throw new InvalidCastException($"Device with sid '{sid}' cannot be converted to {nameof(T)}");

            return device;
        }

        public IEnumerable<T> GetDevicesByType<T>() where T : MiHomeDevice
        {
            return _devicesList.Values.OfType<T>();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _receiveTask?.Wait();
            _transport?.Dispose();
        }
        
        private void ProcessReport(ResponseCommand cmd)
        {
            GetOrAddDeviceByCommand(cmd)?.ParseData(cmd.Data);
        }

        private void ProcessHeartbeat(ResponseCommand cmd)
        {
            if (_gateway != null && cmd.Sid == _gateway.Sid)
            {
                _transport.SetToken(cmd.Token);
                _gateway.ParseData(cmd.Data);
            }
            else if(cmd.Model != "gateway") 
            {
                GetOrAddDeviceByCommand(cmd)?.ParseData(cmd.Data);
            }
        }

        private void ProcessReadAck(ResponseCommand cmd)
        {
            if(cmd.Model != "gateway") GetOrAddDeviceByCommand(cmd);
        }

        private MiHomeDevice GetOrAddDeviceByCommand(ResponseCommand cmd)
        {
            if (_gateway != null && cmd.Sid == _gateway.Sid) return null;

            if (_devicesList.ContainsKey(cmd.Sid)) return _devicesList[cmd.Sid];
            
            if (!_devicesMap.ContainsKey(cmd.Model))
            {
                _logger.LogWarning($"Device '{cmd.Model}' is not supported in the library. Please send a feature request to author.");
                return null;
            }

            var device = _devicesMap[cmd.Model](cmd.Sid);

            if (_namesMap != null && _namesMap.ContainsKey(cmd.Sid)) device.Name = _namesMap[cmd.Sid];

            if (cmd.Data != null) device.ParseData(cmd.Data);

            _devicesList.TryAdd(cmd.Sid, device);

            return device;
        }

        private void DiscoverGatewayAndDevices(ResponseCommand cmd)
        {
            if (_gatewaySid == null)
            {
                if (_gateway == null)
                {
                    _gateway = new Gateway(cmd.Sid, _transport);
                }

                _transport.SetToken(cmd.Token);
            }
            else if (_gatewaySid == cmd.Sid)
            {
                _gateway = new Gateway(cmd.Sid, _transport);
                _transport.SetToken(cmd.Token);
            }

            if (_gateway == null) return;

            _transport.SendCommand(new ReadDeviceCommand(cmd.Sid));

            foreach (var sid in JArray.Parse(cmd.Data))
            {
                _transport.SendCommand(new ReadDeviceCommand(sid.ToString()));

                Task.Delay(ReadDeviceInterval).Wait(); // need some time in order not to loose message
            }
        }
    }
}