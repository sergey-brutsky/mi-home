using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class AsyncBleEventMethodProcessor : IActionProcessor
{
    public const string ACTION = "_async.ble_event";
    private readonly ILogger _logger;
    private readonly MqttDeviceDispatcher _dispatcher;

    public AsyncBleEventMethodProcessor(GatewayDevices devices, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<AsyncBleEventMethodProcessor>();
        _dispatcher = new MqttDeviceDispatcher(devices, _logger, ACTION);
    }

    public void ProcessMessage(JsonElement json)
    {
        if (MqttJson.TryRead(() => ReadMessage(json), _logger, ACTION, out var message))
            _dispatcher.Dispatch(message.Did, message.Payload, message.Time);
    }

    private static (string Did, JsonElement Payload, DateTime Time) ReadMessage(JsonElement json)
    {
        var parameters = MqttJson.Object(MqttJson.Object(json).GetProperty("params"));
        var did = MqttJson.String(MqttJson.Object(parameters.GetProperty("dev")).GetProperty("did"));
        var time = MqttJson.Number(parameters.GetProperty("gwts")).UnixSecondsToDateTime();
        // ProtocolMessageReader validates individual events without discarding the rest of the batch.
        return (did, parameters, time);
    }
}
