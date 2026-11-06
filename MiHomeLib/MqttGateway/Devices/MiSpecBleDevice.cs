using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// BLE device whose reports come in miot format: properties as siid/piid, events as siid/eiid.
/// </summary>
public abstract class MiSpecBleDevice(string did, ILoggerFactory loggerFactory) : BleDevice(did, loggerFactory)
{
    /// <summary>
    /// Handlers for property reports, keyed by (siid, piid)
    /// </summary>
    protected Dictionary<(int siid, int piid), Action<JsonNode>> Properties = [];

    /// <summary>
    /// Handlers for event reports, keyed by (siid, eiid)
    /// </summary>
    protected Dictionary<(int siid, int eiid), Action> Events = [];

    protected internal override void ParseData(string data)
    {
        JsonObject @params = JsonNode.Parse(data).AsObject();

        // _async.ble_event payloads carry evt/eid instead of siid, they are not meant for miot devices
        if (!@params.ContainsKey("siid"))
        {
            _logger.LogWarning($"Message '{data}' is not in miot format (no siid). Processing is skipped");
            return;
        }

        var siid = @params["siid"].GetValue<int>();

        if (@params.ContainsKey("piid"))
        {
            var piid = @params["piid"].GetValue<int>();

            if (Properties.TryGetValue((siid, piid), out var propertyAction))
            {
                propertyAction(@params["value"]);
            }
        }
        else if (@params.ContainsKey("eiid"))
        {
            var eiid = @params["eiid"].GetValue<int>();

            if (Events.TryGetValue((siid, eiid), out var eventAction))
            {
                eventAction();
            }
        }
    }
}
