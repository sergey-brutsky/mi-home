using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;

namespace MiHomeLib.XiaomiGateway2.Devices;

public abstract class ManageableXiaomiGateway2SubDevice(string sid, int shortId, IMessageTransport transport, string gwPassword, ILoggerFactory loggerFactory) : XiaomiGateway2SubDevice(sid, shortId, loggerFactory)
{
    protected readonly IMessageTransport _transport = transport;
    protected readonly string _gwPassword = gwPassword;
}