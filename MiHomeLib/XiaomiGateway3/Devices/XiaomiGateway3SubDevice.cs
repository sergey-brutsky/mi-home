using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;

public abstract class XiaomiGateway3SubDevice(string did, ILoggerFactory loggerFactory)
{
    public string Did { get; } = did;    
    public override string ToString() => $"Did: {Did}";
    public DateTime LastTimeMessageReceived { get; internal set; }
    protected readonly ILogger _logger = loggerFactory.CreateLogger<XiaomiGateway3SubDevice>();    
    protected internal abstract void ParseData(string command);    
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {Did}, ";
}