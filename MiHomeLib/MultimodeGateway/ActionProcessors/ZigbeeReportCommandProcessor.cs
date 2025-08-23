using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeLib.MultimodeGateway.ActionProcessors;

public class ZigbeeReportCommandProcessor: IActionProcessor
{
    public const string ACTION = "report";
    private readonly Dictionary<string, MultimodeGatewaySubDevice> _devices;
    private readonly ILogger _logger;

    public ZigbeeReportCommandProcessor(Dictionary<string, MultimodeGatewaySubDevice> devices, ILoggerFactory loggerFactory)
    {
        _devices = devices;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    public void ProcessMessage(JsonNode json)
    {
        var did = json["did"].ToString();

        if (_devices.ContainsKey(did))
        {
            _devices[did].LastTimeMessageReceived = json["time"].GetValue<double>().UnixMilliSecondsToDateTime();
            (_devices[did] as ZigBeeDevice).ParseData((json["mi_spec"] is not null ? json["mi_spec"] : json["params"]).ToString());
        }
        else
        {
            _logger.LogWarning($"Did '{did}' is unknown. Cannot process '{ACTION}' command for this device.");
        }
    }
}
