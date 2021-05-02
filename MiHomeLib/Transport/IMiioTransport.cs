using System;
using System.Threading.Tasks;

namespace MiHomeLib.Devices
{
    public interface IMiioTransport: IDisposable
    {
        public string Ip { get;}
        public string Token { get;}
        string SendMessage(string msg);
        Task<string> SendMessageAsync(string msg);
    }
}