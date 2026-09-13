using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeLib.MqttGateway.ActionProcessors;

public class PropertiesChangedMethodProcessor(Dictionary<string, MqttGatewaySubDevice> devices, ILoggerFactory loggerFactory) : IActionProcessor
{
    public const string ACTION = "properties_changed";
    private readonly Dictionary<string, MqttGatewaySubDevice> _devices = devices;
    private readonly ILogger _logger = loggerFactory.CreateLogger<AsyncBleEventMethodProcessor>();

    public void ProcessMessage(JsonNode json)
    {
        if (!json.AsObject().ContainsKey("params"))
        {
            _logger.LogWarning($"Json string --> '{json}' is not valid for ble parsing");
            return;
        }
        
        var @params = json["params"].AsArray();

        foreach(var obj in @params)
        {
            var did = obj["did"].ToString();

            if (!_devices.TryGetValue(did, out var device))
            {
                _logger.LogWarning($"Device with did '{did}' is unknown. Processing is skipped");
                return;
            }

            device.LastTimeMessageReceived = DateTime.Now;
            device.ParseData(obj.ToString());
        }
    }
}
