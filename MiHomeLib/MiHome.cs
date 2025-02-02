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

namespace MiHomeLib;

[Obsolete("Please use XiaomiGateway2 instead")]
public class MiHome : IDisposable
{
    private static ILogger _logger;
    private static ILoggerFactory _loggerFactory;

    private Gateway _gateway;
    private readonly string _gatewaySid;
    private static IMessageTransport _transport;
    private readonly MiHomeDeviceFactory _miHomeDeviceFactory;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    private readonly Dictionary<Type, Action<MiHomeDevice>> _deviceEvents = new Dictionary<Type, Action<MiHomeDevice>>();
    
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

    public event EventHandler<MiHomeDevice> OnAnyDevice;
    public event EventHandler<Gateway> OnGateway;

    public event EventHandler<AqaraVirationSensor> OnAqaraVirationSensor;
    public event EventHandler<AqaraCubeSensor> OnAqaraCubeSensor;
    public event EventHandler<AqaraMotionSensor> OnAqaraMotionSensor;
    public event EventHandler<AqaraOpenCloseSensor> OnAqaraOpenCloseSensor;
    public event EventHandler<DoorWindowSensor> OnDoorWindowSensor;
    public event EventHandler<MotionSensor> OnMotionSensor;
    public event EventHandler<SmokeSensor> OnSmokeSensor;        
    public event EventHandler<SocketPlug> OnSocketPlug;
    public event EventHandler<Switch> OnSwitch;
    public event EventHandler<ThSensor> OnThSensor;
    public event EventHandler<WaterLeakSensor> OnWaterLeakSensor;
    public event EventHandler<WeatherSensor> OnWeatherSensor;
    public event EventHandler<WiredDualWallSwitch> OnWiredDualWallSwitch;
    public event EventHandler<WirelessDualWallSwitch> OnWirelessDualWallSwitch;

    public MiHome(string gatewayPassword = null, string gatewaySid = null)
    {
        _gatewaySid = gatewaySid;

        _commandsToActions = new Dictionary<ResponseCommandType, Action<ResponseCommand>>
        {
            {ResponseCommandType.GetIdListAck, DiscoverGatewayAndDevices},
            {ResponseCommandType.ReadAck, ProcessReadAck},
            {ResponseCommandType.Report, ProcessReport},
            {ResponseCommandType.Hearbeat, ProcessHeartbeat},
        };

        _deviceEvents = new Dictionary<Type, Action<MiHomeDevice>>
        {
            { typeof(AqaraVirationSensor), x => OnAqaraVirationSensor?.Invoke(this, x as AqaraVirationSensor)},
            { typeof(AqaraCubeSensor), x => OnAqaraCubeSensor?.Invoke(this, x as AqaraCubeSensor)},
            { typeof(AqaraMotionSensor), x => OnAqaraMotionSensor?.Invoke(this, x as AqaraMotionSensor)},
            { typeof(AqaraOpenCloseSensor), x => OnAqaraOpenCloseSensor?.Invoke(this, x as AqaraOpenCloseSensor)},
            { typeof(DoorWindowSensor), x => OnDoorWindowSensor?.Invoke(this, x as DoorWindowSensor)},
            { typeof(MotionSensor), x => OnMotionSensor?.Invoke(this, x as MotionSensor)},
            { typeof(SmokeSensor), x => OnSmokeSensor?.Invoke(this, x as SmokeSensor)},
            { typeof(SocketPlug), x => OnSocketPlug?.Invoke(this, x as SocketPlug)},
            { typeof(Switch), x => OnSwitch?.Invoke(this, x as Switch)},
            { typeof(ThSensor), x => OnThSensor?.Invoke(this, x as ThSensor)},
            { typeof(WaterLeakSensor), x => OnWaterLeakSensor?.Invoke(this, x as WaterLeakSensor)},
            { typeof(WeatherSensor), x => OnWeatherSensor?.Invoke(this, x as WeatherSensor)},
            { typeof(WiredDualWallSwitch), x => OnWiredDualWallSwitch?.Invoke(this, x as WiredDualWallSwitch)},
            { typeof(WirelessDualWallSwitch), x => OnWirelessDualWallSwitch?.Invoke(this, x as WirelessDualWallSwitch)},
        };

        _transport = new UdpTransport(gatewayPassword);

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

    [Obsolete("Use OnGateway event instead")]
    public Gateway GetGateway()
    {
        return _gateway;
    }

    [Obsolete("Use OnAnyDevice event instead")]
    public IReadOnlyCollection<MiHomeDevice> GetDevices()
    {
        return (IReadOnlyCollection<MiHomeDevice>) _devicesList.Values;
    }

    [Obsolete("Use specific event, for example OnThSensor event")]
    public T GetDeviceByName<T>(string name) where T : MiHomeDevice
    {
        var device = _devicesList.Values.FirstOrDefault(x => x.Name == name);

        if (device == null) return null;

        if (device is T d) return d;

        throw new InvalidCastException($"Device with name '{name}' cannot be converted to {nameof(T)}");
    }

    [Obsolete("Use specific event, for example OnThSensor event")]
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

    [Obsolete("Use specific event, for example OnThSensor event")]
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
                Console.WriteLine(str);
                var respCmd = ResponseCommand.FromString(str);

                if (LogRawCommands) _logger?.LogInformation(str);

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

            if (_devicesList.TryAdd(cmd.Sid, device))
            {
                OnAnyDevice?.Invoke(this, device);

                if(_deviceEvents.ContainsKey(device.GetType()))
                {
                    _deviceEvents[device.GetType()](device);
                }
            }

            return device;
        }
        catch (ModelNotSupportedException e)
        {
            _logger?.LogWarning(e, "Model is unknown");

            return null;
        }
    }

    private void DiscoverGatewayAndDevices(ResponseCommand cmd)
    {
        if (_gatewaySid != null && _gatewaySid != cmd.Sid)
        {
            throw new Exception("Gateway is not discovered, make sure that it is powered on");
        }

        _transport.Token = cmd.Token;

        _gateway = new Gateway(cmd.Sid, _transport);
        OnGateway?.Invoke(this, _gateway);

        _transport.SendCommand(new ReadDeviceCommand(cmd.Sid));

        foreach (var sid in JArray.Parse(cmd.Data))
        {
            _transport.SendCommand(new ReadDeviceCommand(sid.ToString()));

            Task.Delay(ReadDeviceInterval).Wait(); // need some time in order not to loose message
        }
    }
}