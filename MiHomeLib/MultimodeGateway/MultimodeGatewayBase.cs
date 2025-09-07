// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mgl03
// Your contributions are appreciated

using System;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Reflection;
using MiHomeLib.MiioDevices;
using MiHomeLib.Transport;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.ActionProcessors;
using MiHomeLib.MultimodeGateway.Devices;
using System.Text.Json;

namespace MiHomeLib.MultimodeGateway;

public abstract class MultimodeGatewayBase : MiotGenericDevice, IDisposable
{       
    public uint UptimeSeconds { get; private set; }
    public string MiioVersion { get; private set; }
    public string Mac { get; private set; }
    public string FirmwareVersion { get; private set; }
    public string Hardware { get; private set; }
    public WifiSettings Wifi { get; private set; }
    public NetifSettings Network { get; private set; }
    protected static ILoggerFactory _loggerFactory = new NullLoggerFactory();
    protected static ILogger<MultimodeGatewayBase> _logger = _loggerFactory.CreateLogger<MultimodeGatewayBase>();
    protected IDevicesDiscoverer _devicesDiscoverer;
    protected string _did;
    private readonly IMqttTransport _mqttTransport;
    private readonly Dictionary<string, IActionProcessor> _supportedActionProcessors;
    private readonly Dictionary<string, Func<string, MultimodeGatewaySubDevice>> _supportedModels = [];
    private readonly Dictionary<int, string> _pdidToModel = [];
    private readonly Dictionary<string, MultimodeGatewaySubDevice> _devices = [];
    public event Func<MultimodeGatewaySubDevice, Task> OnDeviceDiscoveredAsync = (_) => Task.CompletedTask;
    protected static readonly string _zigbeeCommandsTopic = "zigbee/recv";
    protected static readonly string[] _zigbeeTopics = ["zigbee/send"];
    protected static readonly string[] _bleTopics = ["miio/report", "central/report"];
    public MultimodeGatewayBase(string ip, string token, string did = "", int port = 1883) :
        this(
                did,
                new MiioTransport(ip, token),
                new MqttDotNetTransport(ip, port, [.. _zigbeeTopics, .. _bleTopics], _zigbeeCommandsTopic, _loggerFactory),
                new BaseDevicesDiscoverer(ip, 23)
            ) {}
    internal MultimodeGatewayBase(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer) : base(miioTransport, 0)
    {
        _did = did;
        _supportedActionProcessors = new()
        {
            { ZigbeeReportCommandProcessor.ACTION, new ZigbeeReportCommandProcessor(_devices, _loggerFactory) },
            { ZigbeeHeartBeatCommandProcessor.ACTION, new ZigbeeHeartBeatCommandProcessor(_devices, _loggerFactory) },
            { AsyncBleEventMethodProcessor.ACTION, new AsyncBleEventMethodProcessor(_devices, _loggerFactory) }
        };

        _mqttTransport = mqttTransport;
        _devicesDiscoverer = devicesDiscoverer;

        // Building map of the supported devices via reflection props 
        foreach (Type type in Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && (x.IsSubclassOf(typeof(ZigBeeDevice)) || x.IsSubclassOf(typeof(BleDevice)))))
        {
            var bindFlags = BindingFlags.Public | BindingFlags.Static;
            var model = type.GetField("MODEL", bindFlags).GetValue(type).ToString();

            MultimodeGatewaySubDevice addDevice(string did) =>
                type.IsSubclassOf(typeof(ZigBeeManageableDevice)) || type.IsSubclassOf(typeof(ZigBeeManageableBatteryDevice)) ?
                    Activator.CreateInstance(type, did, _mqttTransport, _loggerFactory) as MultimodeGatewaySubDevice :
                    Activator.CreateInstance(type, did, _loggerFactory) as MultimodeGatewaySubDevice;

            _supportedModels.Add(model, addDevice);

            if (!type.IsSubclassOf(typeof(BleDevice))) continue;

            _pdidToModel.Add((int)type.GetField("PDID", bindFlags).GetValue(type), model);
        }

        var response = _miioTransport.SendMessage(BuildParamsArray("miIO.info", string.Empty));

        var values = JsonNode
            .Parse(response)["result"]
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        UptimeSeconds = uint.Parse(values["uptime"]);
        MiioVersion = values["miio_ver"].ToString();
        Mac = values["mac"].ToString();
        FirmwareVersion = values["fw_ver"].ToString();
        Hardware = values["hw_ver"].ToString();

        var apValues = JsonNode
            .Parse(values["ap"].ToString())
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        Wifi = new WifiSettings()
        {
            Ssid = apValues["ssid"].ToString(),
            Bssid = apValues["bssid"].ToString(),
            Rssi = int.Parse(apValues["rssi"]),
            Freq = int.Parse(apValues["freq"]),
        };

        var netifValues = JsonNode
            .Parse(values["netif"].ToString())
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        Network = new NetifSettings()
        {
            Ip = netifValues["localIp"].ToString(),
            Mask = netifValues["mask"].ToString(),
            Gateway = netifValues["gw"].ToString(),
        };
    }
    public void DiscoverDevices()
    {
        var action = string.Empty;

        _mqttTransport.OnMessageReceived += (topic, msg) =>
        {
            _logger.LogInformation($"{topic} --> {msg}");

            var json = JsonNode.Parse(msg);

            switch (topic)
            {
                case var _ when _zigbeeTopics.Contains(topic):
                    action = json["cmd"].ToString();
                    break;
                case var _ when _bleTopics.Contains(topic):
                    action = json["method"].ToString();
                    break;
                default:
                    _logger.LogWarning($"Topic '{topic}' is not supported. Please contribute to support.");
                    break;
            }

            if (!_supportedActionProcessors.ContainsKey(action))
            {
                _logger.LogWarning($"Command/Method '{action}' is unknown. Please contribute to support.");
                return;
            }

            _supportedActionProcessors[action].ProcessMessage(json);
        };

        DiscoverZigbeeDevices().Wait();
        DiscoverBleDevices().Wait();
    }
    public static void SetLoggerFactory(ILoggerFactory value)
    {
        if (value is null) return;

        _loggerFactory = value;
        _logger = _loggerFactory.CreateLogger<MultimodeGatewayBase>();
    }
    public List<MultimodeGatewaySubDevice> GetDevices() => [.. _devices.Values];
    public T GetDeviceByDid<T>(string did) where T : MultimodeGatewaySubDevice
    {
        if (!_devices.ContainsKey(did)) return null;

        var device = _devices[did];

        if (device is not T) return null;

        return device as T;
    }
    public new void Dispose()
    {
        _mqttTransport?.Dispose();
        base.Dispose();
    }
    private async Task DiscoverZigbeeDevices()
    {
        var zigbeeDeviceList = _devicesDiscoverer.DiscoverZigBeeDevices();

        if (zigbeeDeviceList.Count == 0)
        {
            _logger.LogWarning($"Gateway '{_miioTransport.Ip}' has no connected zigbee devices");
            return;
        }

        foreach (var device in zigbeeDeviceList)
        {
            var model = device.model;

            if (!_supportedModels.ContainsKey(model))
            {
                _logger.LogWarning($"Device '{model}' is not supported yet. Please contribute to support");
                continue;
            }

            var did = device.did;
            var mihomeDevice = _supportedModels[model](did) as ZigBeeDevice;

            _logger.LogInformation($"Device '{model}' with did '{did}' has been discovered");

            var props = mihomeDevice.GetProps();
            var sendMsg = BuildParamsArray("get_device_prop", [did, .. props]);
            var json = JsonNode.Parse(_miioTransport.SendMessageRepeated(sendMsg));
            mihomeDevice.LastTimeMessageReceived = DateTime.Now;

            if (json.AsObject().ContainsKey("error") || json["code"].GetValue<int>() != 0)
            {
                _logger.LogError($"Device '{did}' is not responding on '{sendMsg}' or doesn't support miio protocol");
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
                            _logger.LogWarning($"Cannot get property '{props[i]}' for device did '{did}'");
                        }
                    }

                    if (propsCounter == props.Length) mihomeDevice.SetProps(propsArr);
                }
            }

            _devices.Add(did, mihomeDevice);

            await OnDeviceDiscoveredAsync(mihomeDevice);
        }
    }
    private async Task DiscoverBleDevices()
    {
        foreach (var (did, pdid, mac) in _devicesDiscoverer.DiscoverBleDevices())
        {
            if (!_pdidToModel.ContainsKey(pdid))
            {
                _logger.LogWarning($"Device with pdid '{pdid}' is not supported yet. Please contribute to support.");
                continue;
            }

            var model = _pdidToModel[pdid];
            var mihomeDevice = _supportedModels[model](did) as BleDevice;

            mihomeDevice.Mac = DecodeMacAddress(mac);

            _devices.Add(mihomeDevice.Did, mihomeDevice);
            _logger.LogInformation($"Device '{model}' with did '{mihomeDevice.Did}' has been successfully discovered");
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
    public class WifiSettings
    {
        public string Ssid { get; internal set; }
        public string Bssid { get; internal set; }
        public int Rssi { get; internal set; }
        public int Freq { get; internal set; }
    }
    public class NetifSettings
    {
        public string Ip { get; internal set; }
        public string Mask { get; internal set; }
        public string Gateway { get; internal set; }
    }
    /// <summary>
    /// Enable lock to prevent accidental gateway deletion from you smart home system
    /// </summary>
    public bool AccidentalDeletionEnabled
    {
        get => short.Parse(GetMiotProperty(7, 1, _did)) == 1;

        set => SetMiotProperty(7, 1, _did, value ? 1 : 0);
    }
}