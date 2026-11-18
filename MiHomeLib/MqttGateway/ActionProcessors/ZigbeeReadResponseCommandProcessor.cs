using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class ZigbeeReadResponseCommandProcessor : IActionProcessor
{
    public const string ACTION = "read_rsp";
    private readonly ILogger _logger;
    private readonly MqttDeviceDispatcher _dispatcher;

    public ZigbeeReadResponseCommandProcessor(GatewayDevices devices, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ZigbeeReadResponseCommandProcessor>();
        _dispatcher = new MqttDeviceDispatcher(devices, _logger, ACTION);
    }

    public void ProcessMessage(JsonElement json)
    {
        if (!MqttJson.TryRead(() => ReadMessage(json), _logger, ACTION, out var message)) return;
        var values = new List<JsonElement>();
        if (MqttJson.HasValue(message.Results))
        {
            foreach (var item in message.Results.EnumerateArray())
            {
                if (MqttJson.TryRead(() => ReadResult(item, message.Did), _logger, ACTION, out var result) &&
                    MqttJson.HasValue(result)) values.Add(result);
            }
        }
        // Answers about the gateway itself (lumi.0) and unsupported devices are expected.
        _dispatcher.Dispatch(message.Did, values, message.Time);
    }

    private static (string Did, JsonElement Results, DateTime? Time) ReadMessage(JsonElement json)
    {
        var message = MqttJson.Object(json);
        var did = MqttJson.String(message.GetProperty("did"));
        var results = MqttJson.Optional(message, "results");
        if (MqttJson.HasValue(results)) MqttJson.Array(results);
        DateTime? timestamp = null;
        var time = MqttJson.Optional(message, "time");
        if (time.ValueKind == JsonValueKind.Number && time.TryGetDouble(out var milliseconds))
            timestamp = milliseconds.UnixMilliSecondsToDateTime();
        return (did, results, timestamp);
    }

    private JsonElement ReadResult(JsonElement item, string did)
    {
        var result = MqttJson.Object(item);
        var errorValue = MqttJson.Optional(result, "error_code");
        var error = MqttJson.HasValue(errorValue) ? errorValue.GetInt32() : 0;
        var resource = MqttJson.Optional(result, "res_name");
        if (!MqttJson.HasValue(resource) || error != 0 || !MqttJson.HasValue(MqttJson.Optional(result, "value")))
        {
            _logger.LogDebug("Device '{Did}' did not return resource '{Resource}', error code {ErrorCode}",
                did, resource.ToString(), error);
            return default;
        }
        MqttJson.String(resource);
        return result;
    }
}
