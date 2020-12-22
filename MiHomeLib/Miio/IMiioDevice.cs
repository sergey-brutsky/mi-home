using System;
using System.Threading.Tasks;

namespace MiHomeLib.Devices
{
    public interface IMiioDevice: IDisposable
    {
        string SendMessage(string msg);
        Task<string> SendMessageAsync(string msg);
    }
}