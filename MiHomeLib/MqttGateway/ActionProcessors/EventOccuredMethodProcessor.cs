using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeLib.MqttGateway.ActionProcessors;

public class EventOccuredMethodProcessor(Dictionary<string, MqttGatewaySubDevice> devices, ILoggerFactory loggerFactory) : IActionProcessor
{
    public const string ACTION = "event_occured";
    private readonly Dictionary<string, MqttGatewaySubDevice> _devices = devices;
    private readonly ILogger _logger = loggerFactory.CreateLogger<AsyncBleEventMethodProcessor>();

    public void ProcessMessage(JsonNode json)
    {
        if (!json.AsObject().ContainsKey("params"))
        {
            _logger.LogWarning($"Json string --> '{json}' is not valid for ble parsing");
            return;
        }
        
        var parms = json["params"].AsObject();
        
        if (!parms.ContainsKey("did"))
        {
            _logger.LogWarning($"json --> {json} has no 'did' property. Futher processing is impossible");
            return;
        }

        var did = parms["did"].ToString();
        
        if (!_devices.ContainsKey(did))
        {
            _logger.LogWarning($"Device with did '{did}' is unknown. Processing is skipped");
            return;
        }
        
        _devices[did].LastTimeMessageReceived = DateTime.Now;
        _devices[did].ParseData(parms.ToString());
    }
}
