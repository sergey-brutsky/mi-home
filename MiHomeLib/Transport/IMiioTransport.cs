using System;
using System.Threading.Tasks;

namespace MiHomeLib;

public interface IMiioTransport: IDisposable
{
    public string Ip { get;}
    public string Token { get;}
    string SendMessageRepeated(string msg, int times = 3);
    string SendMessage(string msg);
    Task<string> SendMessageAsync(string msg);
}