using System;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices;

public interface IMiioTransport: IDisposable
{
    public string Ip { get;}
    public string Token { get;}
    /// <summary>
    /// Sends the message up to <paramref name="times"/> times. <see cref="SendMessage"/> and <see cref="SendMessageAsync"/>
    /// repeat only requests known to be safe within their timeout. This explicit legacy override may repeat actions too; the library does not use it anymore
    /// </summary>
    [Obsolete("SendMessage and SendMessageAsync repeat safe requests themselves. This method will be removed in the next major version")]
    string SendMessageRepeated(string msg, int times = 3);
    string SendMessage(string msg);
    Task<string> SendMessageAsync(string msg);
}