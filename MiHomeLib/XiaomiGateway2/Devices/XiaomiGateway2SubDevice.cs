using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public abstract class XiaomiGateway2SubDevice(string sid, int shortId, ILoggerFactory loggerFactory)
{
    protected readonly ILogger _logger = loggerFactory.CreateLogger<XiaomiGateway2SubDevice>();
    public string Sid { get; private set; } = sid;
    public int ShortId { get; private set; } = shortId;
    protected Dictionary<string, Action<JsonElement>> Actions = [];
    protected internal virtual void ParseData(string data)
    {
        LastTimeMessageReceived = DateTime.Now;

        var listProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data);

        foreach (var prop in listProps)
        {
            if (Actions.ContainsKey(prop.Key))
            {
                Actions[prop.Key](prop.Value);
            }
            else
            {
                _logger.LogWarning($"Property '{prop.Key}' is not supported for this device yet. Please contribute to support.");
            }
        }
    }    
    public DateTime LastTimeMessageReceived { get; internal set; }
    public override string ToString() => $"Sid: {Sid}, Type: {GetType().Name}, Last seen: {LastTimeMessageReceived}";    
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {Sid}, ";
}
