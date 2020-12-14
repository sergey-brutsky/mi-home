using System;
using System.Threading.Tasks;
using MiHomeLib.Commands;

namespace MiHomeLib.Contracts
{
    public interface IMessageTransport: IDisposable
    {
        int SendCommand(Command command);

        int SendWriteCommand(string sid, string type, Command data);

        Task<string> ReceiveAsync();

        string Token { get; set; }
    }
}
