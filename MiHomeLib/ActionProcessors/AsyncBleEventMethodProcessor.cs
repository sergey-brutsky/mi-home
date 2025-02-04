using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.DevicesV3;
using MiHomeLib.Utils;

namespace MiHomeLib.ActionProcessors;

public class AsyncBleEventMethodProcessor(Dictionary<string, XiaomiGateway3SubDevice> devices, ILoggerFactory loggerFactory) : IActionProcessor
{
    public const string ACTION = "_async.ble_event";
    private readonly Dictionary<string, XiaomiGateway3SubDevice> _devices = devices;
    private readonly ILogger _logger = loggerFactory.CreateLogger<AsyncBleEventMethodProcessor>();

    public void ProcessMessage(JsonNode json)
    {
        if (!json.AsObject().ContainsKey("params") || !json["params"].AsObject().ContainsKey("dev"))
        {
            _logger.LogWarning($"Json string --> '{json}' is not valid for ble parsing");
            return;
        }

        var parms = json["params"].AsObject();
        var dev = parms["dev"].AsObject();

        if (!dev.ContainsKey("did"))
        {
            _logger.LogWarning($"json --> {json} has no 'did' property. Futher processing is impossible");
            return;
        }

        var did = dev["did"].ToString();

        if (!_devices.ContainsKey(did))
        {
            _logger.LogWarning($"Device with did '{did}' is unknown. Processing is skipped");
            return;
        }

        _devices[did].LastTimeMessageReceived = parms["gwts"].GetValue<double>().UnixSecondsToDateTime();
        _devices[did].ParseData(parms.ToString());
    }
}
