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
using Newtonsoft.Json.Linq;

namespace MiHomeLib
{
    public class MiHome : IDisposable
    {
        private static ILogger _logger;
        private static ILoggerFactory _loggerFactory;

        private Gateway _gateway;
        private readonly string _gatewaySid;
        private static IMessageTransport _transport;
        private readonly MiHomeDeviceFactory _miHomeDeviceFactory;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public static bool LogRawCommands { set; private get; }

        public static ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = _loggerFactory.CreateLogger<MiHome>();
            }
            get
            {
                return _loggerFactory;
            }
        }

        private readonly ConcurrentDictionary<string, MiHomeDevice> _devicesList =
            new ConcurrentDictionary<string, MiHomeDevice>();

        private readonly Dictionary<string, string> _namesMap;

        private const int ReadDeviceInterval = 100;

        private readonly Dictionary<ResponseCommandType, Action<ResponseCommand>> _commandsToActions;
        private readonly Task _receiveTask;

        public MiHome(string gatewayPassword = null, string gatewaySid = null)
        {
            _commandsToActions = new Dictionary<ResponseCommandType, Action<ResponseCommand>>
            {
                {ResponseCommandType.GetIdListAck, DiscoverGatewayAndDevices},
                {ResponseCommandType.ReadAck, ProcessReadAck},
                {ResponseCommandType.Report, ProcessReport},
                {ResponseCommandType.Hearbeat, ProcessHeartbeat},
            };

            _gatewaySid = gatewaySid;

            _transport = new UdpTransport(new KeyBuilder(gatewayPassword));

            _miHomeDeviceFactory = new MiHomeDeviceFactory(_transport);

            _receiveTask = Task.Run(() => StartReceivingMessagesAsync(_cts.Token), _cts.Token);

            _transport.SendCommand(new DiscoverGatewayCommand());
        }

        public MiHome(Dictionary<string, string> namesMap, string gatewayPassword = null, string gatewaySid = null)
            : this(gatewayPassword, gatewaySid)
        {
            if (namesMap.GroupBy(x => x.Value).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Values in the dictionary must be unique");
            }

            _namesMap = namesMap;
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

        private async Task StartReceivingMessagesAsync(CancellationToken ct)
        {
            // Receive messages
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var str = await _transport.ReceiveAsync().ConfigureAwait(false);
                    var respCmd = ResponseCommand.FromString(str);

                    if(LogRawCommands) _logger?.LogInformation(str);

                    if (!_commandsToActions.TryGetValue(respCmd.Command, out var actionCommand))
                    {
                        _logger?.LogInformation($"Command '{respCmd.RawCommand}' is not a response command, skipping it");
                        continue;
                    }

                    actionCommand(respCmd);
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Unexpected error");
                }
            }
        }

        private void ProcessReport(ResponseCommand cmd)
        {
            GetOrAddDeviceByCommand(cmd)?.ParseData(cmd.Data);
        }

        private void ProcessHeartbeat(ResponseCommand cmd)
        {
            if (cmd.Model != Gateway.TypeKey)
            {
                GetOrAddDeviceByCommand(cmd)?.ParseData(cmd.Data);
            }
            else
            {
                _transport.Token = cmd.Token;
                _gateway.ParseData(cmd.Data);
            }
        }

        private void ProcessReadAck(ResponseCommand cmd)
        {
            if (cmd.Model != Gateway.TypeKey) GetOrAddDeviceByCommand(cmd);
        }

        private MiHomeDevice GetOrAddDeviceByCommand(ResponseCommand cmd)
        {
            if (_devicesList.TryGetValue(cmd.Sid, out var miHomeDevice))
            {
                return miHomeDevice;
            }

            try
            {
                var device = _miHomeDeviceFactory.CreateByModel(cmd.Model, cmd.Sid);

                if (_namesMap != null && _namesMap.TryGetValue(cmd.Sid, out var deviceName))
                {
                    device.Name = deviceName;
                }

                if (cmd.Data != null) device.ParseData(cmd.Data);

                _devicesList.TryAdd(cmd.Sid, device);

                return device;
            }
            catch(ModelNotSupportedException e)
            {
                _logger?.LogWarning(e, "Model is unknown");

                return null;
            }
        }

        private void DiscoverGatewayAndDevices(ResponseCommand cmd)
        {
            if (_gatewaySid == null)
            {
                if (_gateway == null)
                {
                    _gateway = new Gateway(cmd.Sid, _transport);
                }

                _transport.Token = cmd.Token;
            }
            else if (_gatewaySid == cmd.Sid)
            {
                _gateway = new Gateway(cmd.Sid, _transport);
                _transport.Token = cmd.Token;
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