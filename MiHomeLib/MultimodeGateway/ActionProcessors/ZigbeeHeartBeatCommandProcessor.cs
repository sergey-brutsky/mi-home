using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeLib.MultimodeGateway.ActionProcessors;

public class ZigbeeHeartBeatCommandProcessor(Dictionary<string, MultimodeGatewaySubDevice> devices, ILoggerFactory loggerFactory) : IActionProcessor
{
    public const string ACTION = "heartbeat";
    private readonly Dictionary<string, MultimodeGatewaySubDevice> _devices = devices;
    private readonly ILogger _logger = loggerFactory.CreateLogger<ZigbeeHeartBeatCommandProcessor>();

    public void ProcessMessage(JsonNode json)
    {
        var data = json["params"].Deserialize<List<Dictionary<string, JsonElement>>>();

        if (data.Count != 1)
        {
            _logger.LogWarning($"Wrong structure of heartbeat message --> '{data}'." +
            "Processing of such structure is not supported");
            return;
        }

        if (!data[0].ContainsKey("did"))
        {
            _logger.LogWarning("Heartbeat message doesn't contain 'did'." +
            "Processing of such structure is not supported");
            return;
        }

        var did = data[0]["did"].GetString();

        if (_devices.ContainsKey(did))
        {
            _devices[did].LastTimeMessageReceived = data[0]["time"].GetDouble().UnixMilliSecondsToDateTime();
            (_devices[did] as ZigBeeDevice).ParseData(data[0]["res_list"].ToString());
        }
        else
        {
            _logger.LogWarning($"Did '{did}' is unknown. Cannot process '{ACTION}' command for this device.");
        }
    }
}
