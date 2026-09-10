using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib;

/// <summary>
/// Common base class for all gateway sub-devices (both XiaomiGateway2 and MqttGateway hierarchies).
/// Contains shared properties and behavior: logging, last message timestamp, device identification.
/// </summary>
public abstract class GatewaySubDeviceBase(ILoggerFactory loggerFactory)
{
    protected readonly ILogger _logger = loggerFactory.CreateLogger<GatewaySubDeviceBase>();

    /// <summary>
    /// Primary device identifier (Did for MqttGateway devices, Sid for XiaomiGateway2 devices)
    /// </summary>
    public abstract string DeviceId { get; }

    /// <summary>
    /// Timestamp of the last received message from this device
    /// </summary>
    public DateTime LastTimeMessageReceived { get; internal set; }

    /// <summary>
    /// Parse incoming data payload from the gateway
    /// </summary>
    protected internal abstract void ParseData(string command);

    /// <summary>
    /// Format base device info string with market model, model and device identifier
    /// </summary>
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {DeviceId}, ";
}