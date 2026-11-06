using System;

namespace MiHomeLib.Contracts;

public interface IMqttTransport : IDisposable
{
    void SendMessage(string message);
    void SendCommand(string topic, string message);
    event Action<string, string> OnMessageReceived;
}
