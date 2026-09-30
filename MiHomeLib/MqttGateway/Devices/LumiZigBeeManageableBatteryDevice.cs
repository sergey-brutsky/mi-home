using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class LumiZigBeeManageableBatteryDevice(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : LumiZigBeeBatteryDevice(did, loggerFactory)
{
    private readonly ZigBeeTransport _zigBeeTransport = new(mqttTransport);
    protected void SendWriteCommand(string resName, int value) => _zigBeeTransport.SendWriteCommand(Did, resName, value);
}
