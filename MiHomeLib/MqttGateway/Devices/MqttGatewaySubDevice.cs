using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class MqttGatewaySubDevice(string did, ILoggerFactory loggerFactory) : GatewaySubDeviceBase(loggerFactory)
{
    public string Did { get; } = did;
    public override string DeviceId => Did;
    public override string ToString() => $"Did: {Did}";
}