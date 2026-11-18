using MiHomeLib.Devices;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class PropertiesChangedMethodProcessor : IActionProcessor
{
    public const string ACTION = "properties_changed";
    private readonly ILogger _logger;
    private readonly MqttDeviceDispatcher _dispatcher;

    public PropertiesChangedMethodProcessor(GatewayDevices devices, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PropertiesChangedMethodProcessor>();
        _dispatcher = new MqttDeviceDispatcher(devices, _logger, ACTION);
    }

    public void ProcessMessage(JsonElement json)
    {
        if (!MqttJson.TryRead(() => MqttJson.Array(MqttJson.Object(json).GetProperty("params")),
                _logger, ACTION, out var items)) return;
        foreach (var item in items.EnumerateArray())
        {
            // One malformed property must not discard the rest of the batch.
            if (MqttJson.TryRead(() => ReadProperty(item), _logger, ACTION, out var did))
                _dispatcher.Dispatch(did, item, null);
        }
    }

    private static string ReadProperty(JsonElement item)
    {
        var property = MqttJson.Object(item);
        var did = MqttJson.String(property.GetProperty("did"));
        MqttJson.Number(property.GetProperty("siid"));
        MqttJson.Number(property.GetProperty("piid"));
        if (!MqttJson.HasValue(MqttJson.Optional(property, "value")))
            throw new FormatException("Property has no value.");
        return did;
    }
}
