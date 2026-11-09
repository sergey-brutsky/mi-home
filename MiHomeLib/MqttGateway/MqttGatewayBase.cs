using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Contracts;
using MiHomeLib.MiioDevices;
using MiHomeLib.MqttGateway.ActionProcessors;
using MiHomeLib.MqttGateway.Devices;


namespace MiHomeLib.MqttGateway;

public abstract class MqttGatewayBase : MiotGenericDevice, IDisposable
{
    // Default for gateways created without a factory of their own, set by SetLoggerFactory
    private static ILoggerFactory _defaultLoggerFactory = new NullLoggerFactory();

    [Obsolete("Use LoggerFactory, it is the factory the gateway was created with. This field will be removed in the next major version")]
    protected static ILoggerFactory _loggerFactory = _defaultLoggerFactory;

    [Obsolete("Use Logger, it logs through the factory the gateway was created with. This field will be removed in the next major version")]
    protected static ILogger<MqttGatewayBase> _logger = _defaultLoggerFactory.CreateLogger<MqttGatewayBase>();

    /// <summary>
    /// Factory the gateway was created with, also used by all its devices
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Logger of this gateway, its category is the concrete gateway type
    /// </summary>
    protected ILogger Logger { get; }
    protected IDevicesDiscoverer _devicesDiscoverer;
    private readonly IMqttTransport _mqttTransport;
    private readonly Dictionary<string, IActionProcessor> _supportedActionProcessors;
    private readonly Dictionary<string, Type> _supportedModels = [];
    private readonly Dictionary<int, string> _pdidToModel = [];
    private readonly Dictionary<string, MqttGatewaySubDevice> _devices = [];
    private bool _subscribed;
    private readonly object _devicesLock = new();
    public event Func<MqttGatewaySubDevice, Task> OnDeviceDiscoveredAsync = (_) => Task.CompletedTask;
    protected static readonly string _zigbeeCommandsTopic = "zigbee/recv";
    protected static readonly string[] _zigbeeTopics = ["zigbee/send"];
    protected static readonly string[] _bleTopics = ["central/report"];
    protected MqttGatewayBase(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer)
        : this(did, miioTransport, mqttTransport, devicesDiscoverer, null) { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by SetLoggerFactory</param>
    protected MqttGatewayBase(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory) : base(did, miioTransport, 0)
    {
        LoggerFactory = ResolveLoggerFactory(loggerFactory);
        Logger = LoggerFactory.CreateLogger(GetType());

        _supportedActionProcessors = new()
        {
            { ZigbeeReportCommandProcessor.ACTION, new ZigbeeReportCommandProcessor(_devices, LoggerFactory) },
            { ZigbeeHeartBeatCommandProcessor.ACTION, new ZigbeeHeartBeatCommandProcessor(_devices, LoggerFactory) },
            { AsyncBleEventMethodProcessor.ACTION, new AsyncBleEventMethodProcessor(_devices, LoggerFactory) },
            { EventOccuredMethodProcessor.ACTION, new EventOccuredMethodProcessor(_devices, LoggerFactory) },
            { PropertiesChangedMethodProcessor.ACTION, new PropertiesChangedMethodProcessor(_devices, LoggerFactory) },
        };

        _mqttTransport = mqttTransport;
        _devicesDiscoverer = devicesDiscoverer;

        // Building map of the supported devices via reflection
        foreach (var (type, model) in DeviceTypeScanner.FindDeviceTypes(typeof(MiSpecZigBeeDevice), typeof(LumiZigBeeDevice), typeof(BleDevice)))
        {
            _supportedModels.Add(model, type);

            if (!type.IsSubclassOf(typeof(BleDevice))) continue;

            _pdidToModel.Add(DeviceTypeScanner.GetStaticField<int>(type, "PDID"), model);
        }
    }
    // .Wait() keeps the exception type callers already get (AggregateException)
    public void DiscoverDevices() => DiscoverDevicesAsync().Wait();

    public async Task DiscoverDevicesAsync()
    {
        // The subscription and the found devices are kept for the gateway lifetime, so a repeated
        // discovery neither doubles every incoming message nor fails on devices it already knows
        if (!_subscribed)
        {
            _mqttTransport.OnMessageReceived += OnMqttMessageReceived;
            _subscribed = true;
        }

        await DiscoverZigbeeDevices().ConfigureAwait(false);
        await DiscoverBleDevices().ConfigureAwait(false);
        await DiscoverMeshDevices().ConfigureAwait(false);
    }

    private void OnMqttMessageReceived(string topic, string msg)
    {
        Logger.LogDebug("{Topic} --> {Message}", topic, msg);

        var json = JsonNode.Parse(msg);
        string action;

        switch (topic)
        {
            case var _ when _zigbeeTopics.Contains(topic):
                action = json["cmd"].ToString();
                break;
            case var _ when _bleTopics.Contains(topic):
                action = json["method"].ToString();
                break;
            default:
                Logger.LogWarning("Topic '{Topic}' is not supported. Please contribute to support.", topic);
                return;
        }

        if (!_supportedActionProcessors.TryGetValue(action, out var processor))
        {
            Logger.LogWarning("Command/Method '{Action}' is unknown. Please contribute to support.", action);
            return;
        }

        // Discovery adds devices from the caller's thread while processors read them on the MQTT thread
        lock (_devicesLock) processor.ProcessMessage(json);
    }

    protected virtual async Task DiscoverZigbeeDevices()
    {
        var zigbeeDeviceList = _devicesDiscoverer.DiscoverZigBeeDevices();

        if (zigbeeDeviceList.Count == 0)
        {
            Logger.LogWarning("Gateway '{Ip}' has no connected zigbee devices", _miioTransport.Ip);
            return;
        }

        foreach (var device in zigbeeDeviceList)
        {
            var model = device.Model;

            if (!_supportedModels.TryGetValue(model, out var type))
            {
                Logger.LogWarning("Device '{Model}' is not supported yet. Please contribute to support", model);
                continue;
            }

            var did = device.Did;

            if (IsKnownDevice(did)) continue; // already discovered by a previous run

            Logger.LogInformation("Device '{Model}' with did '{Did}' has been discovered", model, did);

            if(type.IsSubclassOf(typeof(MiSpecZigBeeDevice)))
            {
                var mispecDevice = Activator.CreateInstance(type, did, LoggerFactory) as MiSpecZigBeeDevice;
                AddDevice(did, mispecDevice);
                mispecDevice.LastTimeMessageReceived = DateTime.Now;
                await OnDeviceDiscoveredAsync(mispecDevice);
                continue;
            }

            var mihomeDevice = (type.IsSubclassOf(typeof(LumiZigBeeManageableDevice)) || type.IsSubclassOf(typeof(LumiZigBeeManageableBatteryDevice)) ?
                Activator.CreateInstance(type, did, _mqttTransport, LoggerFactory):
                Activator.CreateInstance(type, did, LoggerFactory)) as LumiZigBeeDevice;
            
            var props = mihomeDevice.GetProps();
            var sendMsg = BuildParamsArray("get_device_prop", [did, .. props]);
            var json = JsonNode.Parse(_miioTransport.SendMessageRepeated(sendMsg));
            mihomeDevice.LastTimeMessageReceived = DateTime.Now;

            if (json.AsObject().ContainsKey("error") || json["code"].GetValue<int>() != 0)
            {
                Logger.LogError("Device '{Did}' is not responding on '{Request}' or doesn't support miio protocol", did, sendMsg);
            }
            else
            {
                var propsJson = json["result"] as JsonArray;

                // Device supports getting multiple props simultaneously
                if (propsJson.Count == props.Length)
                {
                    mihomeDevice.SetProps([.. propsJson]);
                }
                else // Need to get properties one by one
                {
                    var propsArr = new JsonNode[props.Length];
                    var propsCounter = 0;

                    for (int i = 0; i < props.Length; i++)
                    {
                        sendMsg = BuildParamsArray("get_device_prop", [did, props[i]]);
                        json = JsonNode.Parse(_miioTransport.SendMessageRepeated(sendMsg));

                        if (json["code"].GetValue<int>() == 0)
                        {
                            propsCounter++;
                            propsArr[i] = (json["result"] as JsonArray)[0];
                        }
                        else
                        {
                            Logger.LogWarning("Cannot get property '{Property}' for device did '{Did}'", props[i], did);
                        }
                    }

                    if (propsCounter == props.Length) mihomeDevice.SetProps(propsArr);
                }
            }

            AddDevice(did, mihomeDevice);

            await OnDeviceDiscoveredAsync(mihomeDevice);
        }
    }
    protected virtual async Task DiscoverBleDevices()
    {
        foreach (var device in _devicesDiscoverer.DiscoverBleDevices())
        {
            if (!_pdidToModel.TryGetValue(device.Pdid, out var model))
            {
                Logger.LogWarning("Device with pdid '{Pdid}' is not supported yet. Please contribute to support.", device.Pdid);
                continue;
            }

            if (IsKnownDevice(device.Did)) continue; // already discovered by a previous run

            var mihomeDevice = Activator.CreateInstance(_supportedModels[model], device.Did, LoggerFactory) as BleDevice;

            mihomeDevice.Mac = DecodeMacAddress(device.Mac);

            AddDevice(mihomeDevice.Did, mihomeDevice);
            Logger.LogInformation("Device '{Model}' with did '{Did}' has been successfully discovered", model, mihomeDevice.Did);
            await OnDeviceDiscoveredAsync(mihomeDevice);
        }
    }
    protected virtual async Task DiscoverMeshDevices()
    {
        foreach (var device in _devicesDiscoverer.DiscoverMeshDevices())
        {
            if (!_pdidToModel.TryGetValue(device.Pdid, out var model))
            {
                Logger.LogWarning("Mesh device with pdid '{Pdid}' is not supported yet. Please contribute to support.", device.Pdid);
                continue;
            }

            if (IsKnownDevice(device.Did)) continue; // already discovered by a previous run

            var mihomeDevice = Activator.CreateInstance(_supportedModels[model], device.Did, LoggerFactory) as MeshDevice;

            // mesh_device_v3 stores MAC already as aa:bb:cc:dd:ee:ff, unlike gateway_authed_table
            mihomeDevice.Mac = device.Mac;
            mihomeDevice.MqttTransport = _mqttTransport;

            AddDevice(mihomeDevice.Did, mihomeDevice);
            Logger.LogInformation("Mesh device '{Model}' with did '{Did}' has been successfully discovered", model, mihomeDevice.Did);
            await OnDeviceDiscoveredAsync(mihomeDevice);
        }
    }
    public string DecodeMacAddress(string mac)
    {
        var chunks = Enumerable
            .Range(0, mac.Length / 2)
            .Select(i => mac.Substring(i * 2, 2))
            .Reverse();

        return string.Join(":", chunks);
    }

    /// <summary>
    /// Enable lock to prevent accidental gateway deletion from you smart home system
    /// </summary>
    public bool AccidentalDeletionEnabled
    {
        get => short.Parse(GetMiotProperty(7, 1, _did)) == 1;

        set => SetMiotProperty(7, 1, _did, value ? 1 : 0);
    }

    /// <summary>
    /// Sets the logger factory for gateways created afterwards without a factory of their own
    /// </summary>
    public static void SetLoggerFactory(ILoggerFactory value)
    {
        if (value is null) return;

        _defaultLoggerFactory = value;

        // The obsolete fields keep following the default for derived classes that still read them
#pragma warning disable CS0618
        _loggerFactory = value;
        _logger = value.CreateLogger<MqttGatewayBase>();
#pragma warning restore CS0618
    }
    private protected static ILoggerFactory ResolveLoggerFactory(ILoggerFactory loggerFactory) => loggerFactory ?? _defaultLoggerFactory;
    private bool IsKnownDevice(string did)
    {
        lock (_devicesLock) return _devices.ContainsKey(did);
    }
    private void AddDevice(string did, MqttGatewaySubDevice device)
    {
        lock (_devicesLock) _devices.Add(did, device);
    }
    public List<MqttGatewaySubDevice> GetDevices()
    {
        lock (_devicesLock) return [.. _devices.Values];
    }
    public T GetDeviceByDid<T>(string did) where T : MqttGatewaySubDevice
    {
        MqttGatewaySubDevice device;
        lock (_devicesLock) _devices.TryGetValue(did, out device);
        if (device is null) return null;

        if (device is not T typed) return null;

        return typed;
    }
    public override void Dispose()
    {
        _mqttTransport?.Dispose();
        base.Dispose();
    }
}