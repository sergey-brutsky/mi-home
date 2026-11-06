using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// Bluetooth Mesh device. Events arrive from the gateway as BLE reports, commands go back to it
/// through miio/command using the same miot payloads as the gateway's own write requests
/// </summary>
public abstract class MeshDevice(string did, ILoggerFactory loggerFactory) : MiSpecBleDevice(did, loggerFactory)
{
    private const string COMMANDS_TOPIC = "miio/command";

    /// <summary>
    /// Set by the gateway after the device is created, used to send commands
    /// </summary>
    internal IMqttTransport MqttTransport { get; set; }

    protected void SetProperty(int siid, int piid, object value)
    {
        SendCommand(new
        {
            method = "set_properties",
            @params = new[] { new { did = Did, siid, piid, value } }
        });
    }

    protected void CallAction(int siid, int aiid)
    {
        SendCommand(new
        {
            method = "action",
            @params = new { did = Did, siid, aiid, @in = Array.Empty<object>() }
        });
    }

    private void SendCommand(object payload)
    {
        if (MqttTransport is null)
        {
            throw new InvalidOperationException($"Device '{Did}' is not connected to the gateway transport");
        }

        MqttTransport.SendCommand(COMMANDS_TOPIC, JsonSerializer.Serialize(payload));
    }
}
