using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public abstract class XiaomiGateway2SubDevice(string sid, int shortId, ILoggerFactory loggerFactory) : GatewaySubDeviceBase(loggerFactory)
{
    public string Sid { get; private set; } = sid;
    public int ShortId { get; private set; } = shortId;
    public override string DeviceId => Sid;
    protected Dictionary<string, Action<JsonElement>> Actions = [];
    protected internal override void ParseData(string data)
    {
        LastTimeMessageReceived = DateTime.Now;

        var listProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data);

        foreach (var prop in listProps)
        {
            if (Actions.TryGetValue(prop.Key, out var action))
            {
                action(prop.Value);
            }
            else
            {
                _logger.LogWarning($"Property '{prop.Key}' is not supported for this device yet. Please contribute to support.");
            }
        }
    }    
    public override string ToString() => $"Sid: {Sid}, Type: {GetType().Name}, Last seen: {LastTimeMessageReceived}";    
}
