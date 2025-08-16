using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // for Moq unit tests

namespace MiHomeLib.Transport;

public interface IMessageTransport: IDisposable
{
    int SendCommand(Command command);

    int SendWriteCommand(string sid, string type, Command data);

    event Action<string> OnMessageReceived;

    string Token { get; internal set; }
}
