using System;

namespace MiHomeLib.Contracts;

public interface IMqttTransport : IDisposable
{
    void SendMessage(string message);
    event Action<string, string> OnMessageReceived;
}
