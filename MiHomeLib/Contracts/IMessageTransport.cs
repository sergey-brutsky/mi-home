using System;
using MiHomeLib.XiaomiGateway2.Commands;

namespace MiHomeLib.Contracts;

public interface IMessageTransport: IDisposable
{
    int SendCommand(Command command);

    int SendWriteCommand(string sid, string type, string gwPassword, Command data);

    event Action<string> OnMessageReceived;

    string Token { get; set; }
}
