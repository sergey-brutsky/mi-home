using System;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace MiHomeLib.Transport;

internal class MqttDotNetTransport : IMqttTransport, IDisposable
{
    private const double TIMEOUT_MS = 5_000; // timeout in milliseconds
    private readonly ILogger _logger;
    private readonly IMqttClient _mqttClient;
    private readonly string _commandsTopic;

    public MqttDotNetTransport(string ip, int port, string[] listenTopics, string commandsTopic, ILoggerFactory loggerFactory)
    {
        _commandsTopic = commandsTopic;
        _logger = loggerFactory.CreateLogger(GetType());
        _mqttClient = new MqttFactory().CreateMqttClient();

        _mqttClient.ApplicationMessageReceivedAsync += x =>
        {
            OnMessageReceived?
                .Invoke(
                    x.ApplicationMessage.Topic, 
                    System.Text.Encoding.UTF8.GetString([.. x.ApplicationMessage.PayloadSegment])
                );

            return System.Threading.Tasks.Task.CompletedTask;
        };

        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(ip, port)
            .Build();
        
        var subscriptionBuilder = new MqttFactory().CreateSubscribeOptionsBuilder();
        
        Array.ForEach(listenTopics, topic => subscriptionBuilder.WithTopicFilter(topic));

        _mqttClient.ConnectAsync(mqttClientOptions).Wait(TimeSpan.FromMilliseconds(TIMEOUT_MS));
        _logger.LogInformation($"MQTT client connected to the broker --> {ip}:{port}");

        _mqttClient.SubscribeAsync(subscriptionBuilder.Build()).Wait(TimeSpan.FromMilliseconds(TIMEOUT_MS));
        _logger.LogInformation($"MQTT client subscribed to the topics --> {string.Join(",", listenTopics)}");
        
    }
    public void SendMessage(string message)
    {
        _mqttClient
            .PublishStringAsync(_commandsTopic, message)
            .Wait(TimeSpan.FromMilliseconds(TIMEOUT_MS));
    }
    public event Action<string, string> OnMessageReceived;
    public void Dispose() => _mqttClient?.Dispose();
}
