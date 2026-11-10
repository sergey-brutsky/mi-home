using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class BleDevice(string did, ILoggerFactory loggerFactory) : MqttGatewaySubDevice(did, loggerFactory)
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
            
            if(EidToActions.TryGetValue(eid, out var action))
            {
                action(evt["edata"].ToString().ToLower());
            }
            else
            {
                _logger.LogWarning("Eid '{Eid}' is not supported for this device yet. Please contribute to support.", eid);
            }
        }
    }

    protected override string GetBaseInfo(string marketModel, string model)
    {
        return base.GetBaseInfo(marketModel, model) + $"Mac: {Mac}, ";
    }

    public int ToBleInt256(string hex)
    {
        var res = 0;
        var start = 0;
        
        foreach (var val in hex.ToByteArray()) res += val*(int)Math.Pow(256, start++);
        
        return res;
    }

    public byte ToBleByte(string hex)
    {
        return hex.ToByteArray()[0];
    }

    public float ToBleFloat(string hex)
    {
        // hex string is little endian signed int16 in tenths.
        // Bytes are combined explicitly: arr.Reverse() binds to MemoryExtensions.Reverse (in-place) since C# 14
        var arr = hex.ToByteArray();
        return (short)(arr[0] | arr[1] << 8) / 10f;
    }
}
