using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class ZigbeeHeartBeatCommandProcessor : IActionProcessor
{
    public const string ACTION = "heartbeat";
    private readonly ILogger _logger;
    private readonly MqttDeviceDispatcher _dispatcher;

    public ZigbeeHeartBeatCommandProcessor(GatewayDevices devices, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ZigbeeHeartBeatCommandProcessor>();
        _dispatcher = new MqttDeviceDispatcher(devices, _logger, ACTION);
    }

    public void ProcessMessage(JsonElement json)
    {
        if (MqttJson.TryRead(() => ReadMessage(json), _logger, ACTION, out var message))
            _dispatcher.Dispatch(message.Did, message.Payload, message.Time);
    }

    private static (string Did, JsonElement Payload, DateTime Time) ReadMessage(JsonElement json)
    {
        var items = MqttJson.Array(MqttJson.Object(json).GetProperty("params"));
        if (items.GetArrayLength() != 1)
            throw new FormatException("Expected exactly one heartbeat item.");
        var item = MqttJson.Object(items[0]);
        var did = MqttJson.String(item.GetProperty("did"));
        var time = MqttJson.Number(item.GetProperty("time")).UnixMilliSecondsToDateTime();
        return (did, MqttJson.Array(item.GetProperty("res_list")), time);
    }
}
