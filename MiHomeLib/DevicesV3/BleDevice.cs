using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public abstract class BleDevice(string did, ILoggerFactory loggerFactory) : XiaomiGateway3SubDevice(did, loggerFactory)
{
    public string Mac { get; internal set; }

    protected Dictionary<int, Action<string>> EidToActions = [];

    protected internal override void ParseData(string data)
    {
        var json = JsonNode.Parse(data);
        var events = json["evt"] as JsonArray;

        foreach (var evt in events)
        {
            var eid = evt["eid"].GetValue<int>();
            
            if(EidToActions.ContainsKey(eid))
            {
                EidToActions[eid](evt["edata"].ToString().ToLower());
            }
            else
            {
                _logger.LogWarning($"Eid '{eid}' is not supported for this device yet. Please contribute to support.");
            }
        }
    }

    protected override string GetBaseInfo(string marketModel, string model)
    {
        return base.GetBaseInfo(marketModel, model) + $"Mac: {Mac}, ";
    }
}
