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
    private readonly ILogger _logger = loggerFactory.CreateLogger<EventOccuredMethodProcessor>();

    public void ProcessMessage(JsonNode json)
    {
        if (!json.AsObject().ContainsKey("params"))
        {
            _logger.LogWarning("Json string --> '{Message}' is not valid for ble parsing", json);
            return;
        }
        
        var parms = json["params"].AsObject();
        
        if (!parms.ContainsKey("did"))
        {
            _logger.LogWarning("json --> {Message} has no 'did' property. Futher processing is impossible", json);
            return;
        }

        var did = parms["did"].ToString();
        
        if (!_devices.TryGetValue(did, out var device))
        {
            _logger.LogWarning("Device with did '{Did}' is unknown. Processing is skipped", did);
            return;
        }
        
        device.LastTimeMessageReceived = DateTime.Now;
        device.ParseData(parms.ToString());
    }
}
