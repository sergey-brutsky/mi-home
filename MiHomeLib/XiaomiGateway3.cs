using System;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.DevicesV3;
using System.Linq;
using System.Reflection;
using MiHomeLib.ActionProcessors;
using MiHomeLib.MiioDevices;
using MiHomeLib.Transport;

namespace MiHomeLib;

public class XiaomiGateway3 : MiioDevice, IDisposable
{
    private static ILoggerFactory _loggerFactory = new NullLoggerFactory();
    private static ILogger<XiaomiGateway3> _logger = _loggerFactory.CreateLogger<XiaomiGateway3>();
    private readonly IDevicesDiscoverer _devicesDiscoverer;
    private readonly IMqttTransport _mqttTransport;     
    private readonly Dictionary<string, IActionProcessor> _supportedActionProcessors;
    private readonly Dictionary<string, Func<string, XiaomiGateway3SubDevice>> _supportedModels = [];
    private readonly Dictionary<int, string> _pdidToModel = [];
    private readonly Dictionary<string, XiaomiGateway3SubDevice> _devices = [];
    public event Action<XiaomiGateway3SubDevice> OnDeviceDiscovered;
    private static readonly string _zigbeeCommandsTopic = "zigbee/recv";
    private static readonly string[] _zigbeeTopics = [ "zigbee/send" ];
    private static readonly string[] _bleTopics = [ "miio/report", "central/report" ];    
    /// <summary>
    /// Xiaomi Multimode Gateway (CN) ZNDMWG03LM
    /// </summary>
    public XiaomiGateway3(string ip, string token, int port = 1883):
        this(
                new MiioTransport(ip, token), 
                new MqttDotNetTransport(ip, port, [.. _zigbeeTopics, .. _bleTopics], _zigbeeCommandsTopic, _loggerFactory),
                new TelnetDevicesDiscoverer(ip, 23)
            ) {}
    internal XiaomiGateway3(IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer) : base(miioTransport)
    {
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
            var model  = type.GetField("MODEL", bindFlags).GetValue(type).ToString();

            XiaomiGateway3SubDevice addDevice(string did) =>
                type.IsSubclassOf(typeof(ZigBeeManageableDevice)) ? 
                    Activator.CreateInstance(type, did, _mqttTransport, _loggerFactory) as XiaomiGateway3SubDevice:
                    Activator.CreateInstance(type, did, _loggerFactory) as XiaomiGateway3SubDevice;

            _supportedModels.Add(model, addDevice);

            if(!type.IsSubclassOf(typeof(BleDevice))) continue;

            _pdidToModel.Add((int)type.GetField("PDID", bindFlags).GetValue(type), model);
        }
    }
    public void DiscoverDevices()
    {
        var action = string.Empty;
            
        _mqttTransport.OnMessageReceived += (topic, msg) => 
        {
            _logger.LogInformation($"{topic} --> {msg}");
            
            var json = JsonNode.Parse(msg);

            switch(topic)
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

            if(!_supportedActionProcessors.ContainsKey(action)) 
            {
                _logger.LogWarning($"Command/Method '{action}' is unknown. Please contribute to support.");
                return;
            }

            _supportedActionProcessors[action].ProcessMessage(json);
        };

        DiscoverZigbeeDevices();
        DiscoverBleDevices();
    }
    public static void SetLoggerFactory(ILoggerFactory value)
    {
        if(value is null) return;

        _loggerFactory = value;            
        _logger = _loggerFactory.CreateLogger<XiaomiGateway3>();
    }
    public List<XiaomiGateway3SubDevice> GetDevices() => [.. _devices.Values];
    public T GetDeviceByDid<T>(string did) where T : XiaomiGateway3SubDevice
    {
        if(!_devices.ContainsKey(did)) return null;

        var device = _devices[did];
        
        if(device is not T) return null;

        return device as T;
    }
    public new void Dispose()
    {
        _mqttTransport?.Dispose();
        base.Dispose();
    }     
    private void DiscoverZigbeeDevices()
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
                if(propsJson.Count == props.Length)
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

                        if(json["code"].GetValue<int>() == 0) 
                        {
                            propsCounter++;
                            propsArr[i] = (json["result"] as JsonArray)[0];
                        }
                        else
                        {
                            _logger.LogWarning($"Cannot get property '{props[i]}' for device did '{did}'");
                        }
                    }

                    if(propsCounter == props.Length) mihomeDevice.SetProps(propsArr);
                }
            }
            
            _devices.Add(did, mihomeDevice);

            OnDeviceDiscovered?.Invoke(mihomeDevice);
        }
    }
    private void DiscoverBleDevices()
    {
        foreach (var (did, pdid, mac) in _devicesDiscoverer.DiscoverBleDevices())
        {
            if(!_pdidToModel.ContainsKey(pdid))
            {
                _logger.LogWarning($"Device with pdid '{pdid}' is not supported yet. Please contribute to support.");
                continue;
            }

            var model = _pdidToModel[pdid];
            var mihomeDevice = _supportedModels[model](did) as BleDevice;

            mihomeDevice.Mac = mac.DecodeMacAddress();

            _devices.Add(mihomeDevice.Did, mihomeDevice);
            _logger.LogInformation($"Device '{model}' with did '{mihomeDevice.Did}' has been successfully discovered");
            OnDeviceDiscovered?.Invoke(mihomeDevice);
        }        
    }
}
