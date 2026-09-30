using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class MiSpecZigBeeDevice(string did, ILoggerFactory loggerFactory) : MqttGatewaySubDevice(did, loggerFactory)
{
    protected Dictionary<(int siid, int eid), Action<JsonArray>> Events = [];
    protected Dictionary<(int siid, int piid), Action<JsonNode>> Properties = [];

    protected internal override void ParseData(string data)
    {
        JsonArray items = JsonNode.Parse(data).AsArray();

        foreach(var item in items)
        {
            var siid = item["siid"].GetValue<int>();
            var eiid = item["eiid"]?.GetValue<int>();
            var piid = item["piid"]?.GetValue<int>();
            
            if (eiid is not null && Events.TryGetValue((siid, eiid.Value), out var action))
            {
                action(item["arguments"]?.AsArray());
            }
            else if (piid is not null && Properties.TryGetValue((siid, piid.Value), out var propAction))
            {
                propAction(item["value"]);
            }
        }      
    }
    public override string ToString() => $"Last seen: {LastTimeMessageReceived}";
}
