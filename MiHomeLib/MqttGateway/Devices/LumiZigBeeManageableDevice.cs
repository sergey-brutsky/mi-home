using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;

namespace MiHomeLib.MqttGateway.Devices;

public abstract class LumiZigBeeManageableDevice(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : LumiZigBeeDevice(did, loggerFactory)
{
    private readonly ZigBeeTransport _zigBeeTransport = new(mqttTransport);
    protected void SendWriteCommand(string resName, int value) => _zigBeeTransport.SendWriteCommand(Did, resName, value);
    protected void SendWriteCommand((int siid, int piid) res, int value) => _zigBeeTransport.SendWriteCommand(Did, res, value);    
}
