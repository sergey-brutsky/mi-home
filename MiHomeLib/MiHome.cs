using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiHomeLib
{
    public class MiHome : IDisposable
    {
        private Gateway _gateway;
        private readonly string _gatewaySid;
        private static UdpTransport _transport;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ConcurrentDictionary<string, MiHomeDevice> _devicesList =
            new ConcurrentDictionary<string, MiHomeDevice>();

        private readonly Dictionary<string, string> _namesMap;

        private const int ReadDeviceInterval = 100;

        private readonly Dictionary<string, Func<string, MiHomeDevice>> _devicesMap =
            new Dictionary<string, Func<string, MiHomeDevice>>
            {
                {ThSensor.SensorKey, sid => new ThSensor(sid)},
                {WeatherSensor.SensorKey, sid => new WeatherSensor(sid)},
                {MotionSensor.SensorKey, sid => new MotionSensor(sid)},
                {SocketPlug.SensorKey, sid => new SocketPlug(sid, _transport)},
                {DoorWindowSensor.SensorKey, sid => new DoorWindowSensor(sid)},
                {WaterLeakSensor.SensorKey, sid => new WaterLeakSensor(sid)},
                {SmokeSensor.SensorKey, sid => new SmokeSensor(sid)},
                {Switch.SensorKey, sid => new Switch(sid)},
                {WiredDualWallSwitch.SensorKey, sid => new WiredDualWallSwitch(sid)},
                {WirelessDualWallSwitch.SensorKey, sid => new WirelessDualWallSwitch(sid)},
                {AqaraCubeSensor.SensorKey, sid => new AqaraCubeSensor(sid)},
                {AqaraMotionSensor.SensorKey, sid => new AqaraMotionSensor(sid)},
                {AqaraOpenCloseSensor.SensorKey, sid => new AqaraOpenCloseSensor(sid)}
            };

        private readonly Dictionary<string, Action<ResponseCommand>> _commandsToActions;
        private readonly Task _receiveTask;

        public MiHome(Dictionary<string, string> namesMap, string gatewayPassword = null, string gatewaySid = null)
            : this(gatewayPassword, gatewaySid)
        {
            if (namesMap.GroupBy(x => x.Value).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Values in the dictionary must be unique");
            }

            _namesMap = namesMap;
        }

        public MiHome(string gatewayPassword = null, string gatewaySid = null)
        {
            _commandsToActions = new Dictionary<string, Action<ResponseCommand>>
            {
                {"get_id_list_ack", DiscoverGatewayAndDevices},
                {"read_ack", ProcessReadAck},
                {"heartbeat", ProcessHeartbeat},
                {"report", ProcessReport},
            };

            _gatewaySid = gatewaySid;

            _transport = new UdpTransport(gatewayPassword);

            _receiveTask = Task.Run(() => StartReceivingMessages(_cts.Token), _cts.Token);

            _transport.SendCommand(new DiscoverGatewayCommand());
        }

        protected MiHome(
            Dictionary<string, Func<string, MiHomeDevice>> devicesMap,
            Dictionary<string, string> namesMap,
            string gatewayPassword = null,
            string gatewaySid = null) : this(namesMap, gatewayPassword, gatewaySid)
        {
            foreach (var pair in devicesMap)
            {
                _devicesMap.Add(pair.Key, pair.Value);
            }
        }

        protected MiHome(
            Dictionary<string, Func<string, MiHomeDevice>> devicesMap,
            string gatewayPassword = null,
            string gatewaySid = null) : this(gatewayPassword, gatewaySid)
        {
            foreach (var pair in devicesMap)
            {
                _devicesMap.Add(pair.Key, pair.Value);
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
            if (!_devicesList.TryGetValue(sid, out var miHomeDevice))
            {
                throw new ArgumentException($"There is no device with sid '{sid}'");
            }

            if (!(miHomeDevice is T device))
            {
                throw new InvalidCastException($"Device with sid '{sid}' cannot be converted to {nameof(T)}");
            }

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

        private async Task StartReceivingMessages(CancellationToken ct)
        {
            // Receive messages
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var str = await _transport.ReceiveAsync().ConfigureAwait(false);

                    var respCmd = JsonConvert.DeserializeObject<ResponseCommand>(str);

                    if (!_commandsToActions.TryGetValue(respCmd.Cmd, out var actionCommand)) continue;

                    actionCommand(respCmd);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
        }

        private void ProcessReport(ResponseCommand cmd)
        {
            GetOrAddDeviceByCommand(cmd).ParseData(cmd.Data);
        }

        private void ProcessHeartbeat(ResponseCommand cmd)
        {
            if (_gateway != null && cmd.Sid == _gateway.Sid)
            {
                _transport.SetToken(cmd.Token);
                _gateway.ParseData(cmd.Data);
            }
            else if (cmd.Model != Gateway.SensorKey)
            {
                GetOrAddDeviceByCommand(cmd).ParseData(cmd.Data);
            }
        }

        private void ProcessReadAck(ResponseCommand cmd)
        {
            if (cmd.Model != Gateway.SensorKey) GetOrAddDeviceByCommand(cmd);
        }

        private MiHomeDevice GetOrAddDeviceByCommand(ResponseCommand cmd)
        {
            if (_gateway != null && cmd.Sid == _gateway.Sid) return _gateway;

            if (_devicesList.TryGetValue(cmd.Sid, out var miHomeDevice))
            {
                return miHomeDevice;
            }

            var device = _devicesMap[cmd.Model](cmd.Sid);

            if (_namesMap != null && _namesMap.TryGetValue(cmd.Sid, out var deviceName))
            {
                device.Name = deviceName;
            }

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