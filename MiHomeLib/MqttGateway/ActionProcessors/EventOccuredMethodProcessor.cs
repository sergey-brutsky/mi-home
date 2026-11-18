using MiHomeLib.Devices;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class EventOccuredMethodProcessor : IActionProcessor
{
    public const string ACTION = "event_occured";
    private readonly ILogger _logger;
    private readonly MqttDeviceDispatcher _dispatcher;

    public EventOccuredMethodProcessor(GatewayDevices devices, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<EventOccuredMethodProcessor>();
        _dispatcher = new MqttDeviceDispatcher(devices, _logger, ACTION);
    }

    public void ProcessMessage(JsonElement json)
    {
        if (MqttJson.TryRead(() => ReadMessage(json), _logger, ACTION, out var message))
            _dispatcher.Dispatch(message.Did, message.Payload, message.Time);
    }

    private static (string Did, JsonElement Payload, DateTime? Time) ReadMessage(JsonElement json)
    {
        var parameters = MqttJson.Object(MqttJson.Object(json).GetProperty("params"));
        var did = MqttJson.String(parameters.GetProperty("did"));
        MqttJson.Number(parameters.GetProperty("siid"));
        MqttJson.Number(parameters.GetProperty("eiid"));
        return (did, parameters, null);
    }
}
