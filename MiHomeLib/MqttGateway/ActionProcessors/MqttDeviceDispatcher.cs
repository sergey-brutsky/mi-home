using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal sealed class MqttDeviceDispatcher(
    GatewayDevices devices, ILogger logger, string action)
{
    public void Dispatch(string did, JsonElement payload, DateTime? timestamp) =>
        Dispatch(did, timestamp, device => device.ParseData(payload));

    public void Dispatch(string did, IReadOnlyCollection<JsonElement> payload, DateTime? timestamp) =>
        Dispatch(did, timestamp, payload.Count > 0 ? device => device.ParseData(payload) : null, expectedUnknown: true);

    private void Dispatch(string did, DateTime? timestamp, Action<GatewaySubDeviceBase> apply, bool expectedUnknown = false)
    {
        if (!devices.TryGet(did, out var device))
        {
            if (expectedUnknown)
                logger.LogDebug("Did '{Did}' is unknown. Skipping '{Action}' command for this device.", did, action);
            else
                logger.LogWarning("Did '{Did}' is unknown. Cannot process '{Action}' command for this device.", did, action);
            return;
        }

        if (apply is not null)
        {
            device.LastTimeMessageReceived = MessageReception.UtcNow;
            device.LastMessageSourceTimeUtc = timestamp?.ToUniversalTime();
        }
        apply?.Invoke(device);
    }
}
