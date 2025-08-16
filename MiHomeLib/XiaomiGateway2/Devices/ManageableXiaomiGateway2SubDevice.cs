using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.XiaomiGateway2.Devices;

public abstract class ManageableXiaomiGateway2SubDevice(string sid, int shortId, IMessageTransport transport, ILoggerFactory loggerFactory) : XiaomiGateway2SubDevice(sid, shortId, loggerFactory)
{
    protected readonly IMessageTransport _transport = transport;
}