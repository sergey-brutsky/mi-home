using System;

namespace MiHomeLib.MqttGateway;

public interface IMqttTransport : IDisposable
{
    void SendMessage(string message);
    void Publish(string topic, string message);
    event Action<string, string> OnMessageReceived;
}
