using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeLib.MqttGateway.ActionProcessors;

public class ZigbeeReportCommandProcessor: IActionProcessor
{
    public const string ACTION = "report";
    private readonly Dictionary<string, MqttGatewaySubDevice> _devices;
    private readonly ILogger _logger;

    public ZigbeeReportCommandProcessor(Dictionary<string, MqttGatewaySubDevice> devices, ILoggerFactory loggerFactory)
    {
        _devices = devices;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    public void ProcessMessage(JsonNode json)
    {
        var did = json["did"].ToString();

        if (_devices.TryGetValue(did, out var device))
        {
            device.LastTimeMessageReceived = json["time"].GetValue<double>().UnixMilliSecondsToDateTime();
            (device as ZigBeeDevice).ParseData((json["mi_spec"] is not null ? json["mi_spec"] : json["params"]).ToString());
        }
        else
        {
            _logger.LogWarning($"Did '{did}' is unknown. Cannot process '{ACTION}' command for this device.");
        }
    }
}
