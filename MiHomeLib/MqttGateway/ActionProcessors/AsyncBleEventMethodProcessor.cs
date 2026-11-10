using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeLib.MqttGateway.ActionProcessors;

public class AsyncBleEventMethodProcessor(Dictionary<string, MqttGatewaySubDevice> devices, ILoggerFactory loggerFactory) : IActionProcessor
{
    public const string ACTION = "_async.ble_event";
    private readonly Dictionary<string, MqttGatewaySubDevice> _devices = devices;
    private readonly ILogger _logger = loggerFactory.CreateLogger<AsyncBleEventMethodProcessor>();

    public void ProcessMessage(JsonNode json)
    {
        if (!json.AsObject().ContainsKey("params") || !json["params"].AsObject().ContainsKey("dev"))
        {
            _logger.LogWarning("Json string --> '{Message}' is not valid for ble parsing", json);
            return;
        }

        var parms = json["params"].AsObject();
        var dev = parms["dev"].AsObject();

        if (!dev.ContainsKey("did"))
        {
            _logger.LogWarning("json --> {Message} has no 'did' property. Futher processing is impossible", json);
            return;
        }

        var did = dev["did"].ToString();

        if (!_devices.TryGetValue(did, out var device))
        {
            _logger.LogWarning("Device with did '{Did}' is unknown. Processing is skipped", did);
            return;
        }

        device.LastTimeMessageReceived = parms["gwts"].GetValue<double>().UnixSecondsToDateTime();
        device.ParseData(parms.ToString());
    }
}
