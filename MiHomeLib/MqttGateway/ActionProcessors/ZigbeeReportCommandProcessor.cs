using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class ZigbeeReportCommandProcessor : IActionProcessor
{
    public const string ACTION = "report";
    private readonly ILogger _logger;
    private readonly MqttDeviceDispatcher _dispatcher;

    public ZigbeeReportCommandProcessor(GatewayDevices devices, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ZigbeeReportCommandProcessor>();
        _dispatcher = new MqttDeviceDispatcher(devices, _logger, ACTION);
    }

    public void ProcessMessage(JsonElement json)
    {
        if (MqttJson.TryRead(() => ReadMessage(json), _logger, ACTION, out var message))
            _dispatcher.Dispatch(message.Did, message.Payload, message.Time);
    }

    private static (string Did, JsonElement Payload, DateTime Time) ReadMessage(JsonElement json)
    {
        var message = MqttJson.Object(json);
        var did = MqttJson.String(message.GetProperty("did"));
        var time = MqttJson.Number(message.GetProperty("time")).UnixMilliSecondsToDateTime();
        var miSpec = MqttJson.Optional(message, "mi_spec");
        var values = MqttJson.Array(MqttJson.HasValue(miSpec) ? miSpec : message.GetProperty("params"));
        return (did, values, time);
    }
}
