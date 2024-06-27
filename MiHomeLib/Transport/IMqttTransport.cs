using System;

namespace MiHomeLib.Transport;

public interface IMqttTransport : IDisposable
{
    void SendMessage(string message);
    event Action<string, string> OnMessageReceived;
}
