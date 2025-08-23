using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway.Devices;

public abstract class ZigBeeManageableDevice(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : ZigBeeDevice(did, loggerFactory)
{
    private readonly ZigBeeTransport _zigBeeTransport = new(mqttTransport);
    protected void SendWriteCommand(string resName, int value) => _zigBeeTransport.SendWriteCommand(Did, resName, value);
    protected void SendWriteCommand((int siid, int piid) res, int value) => _zigBeeTransport.SendWriteCommand(Did, res, value);    
}
