using System;
using System.Threading.Tasks;
using MiHomeLib.Commands;

namespace MiHomeLib.Contracts
{
    public interface IMessageTransport: IDisposable
    {
        int SendCommand(Command cmd);

        int SendWriteCommand(string sid, string type, Command data);

        Task<string> ReceiveAsync();

        void SetToken(string token);
    }
}
