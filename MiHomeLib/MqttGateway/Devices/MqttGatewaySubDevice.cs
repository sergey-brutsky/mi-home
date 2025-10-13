using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class MqttGatewaySubDevice(string did, ILoggerFactory loggerFactory)
{
    public string Did { get; } = did;    
    public override string ToString() => $"Did: {Did}";
    public DateTime LastTimeMessageReceived { get; internal set; }
    protected readonly ILogger _logger = loggerFactory.CreateLogger<MqttGatewaySubDevice>();    
    protected internal abstract void ParseData(string command);    
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {Did}, ";
}