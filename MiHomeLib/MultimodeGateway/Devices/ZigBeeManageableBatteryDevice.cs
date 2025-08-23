using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway.Devices;

public abstract class ZigBeeManageableBatteryDevice(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : ZigBeeBatteryDevice(did, loggerFactory)
{
    private readonly ZigBeeTransport _zigBeeTransport = new(mqttTransport);
    protected void SendWriteCommand(string resName, int value) => _zigBeeTransport.SendWriteCommand(Did, resName, value);
}
