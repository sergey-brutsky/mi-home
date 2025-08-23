using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

public abstract class BleDevice(string did, ILoggerFactory loggerFactory) : MultimodeGatewaySubDevice(did, loggerFactory)
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
        // hex string is little endian !
        var arr = hex.ToByteArray();
        arr.Reverse();
        return BitConverter.ToInt16(arr, 0)/10f;
    }
    
}
