using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using MiHomeLib.Devices;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet;
using MQTTnet.Server;
using System.Text;

namespace MiHomeLib
{
    public class MiHome3 : IDisposable
    {
        private IMqttClient _mqttClient;
        private static ILogger _logger;
        private static ILoggerFactory _loggerFactory;        
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public static ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = _loggerFactory.CreateLogger<MiHome3>();
            }
            get
            {
                return _loggerFactory;
            }
        }

        public MiHome3(string ip, int port = 1883)
        {
            MqttFactory mqttFactory = new MqttFactory();

            _mqttClient = mqttFactory.CreateMqttClient();

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(ip, port)
                .Build();

            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(x => { x.WithTopic("#"); })
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var str = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                //Console.WriteLine(str);
                _logger.LogInformation($"{e.ApplicationMessage.Topic} --> {str}");
                return Task.CompletedTask;
            };

            Task.Run(async () =>
            {
                if(!_cts.IsCancellationRequested)
                {
                    await _mqttClient.ConnectAsync(mqttClientOptions, _cts.Token);
                    _logger.LogInformation("MQTT client connected");
                    await _mqttClient.SubscribeAsync(mqttSubscribeOptions, _cts.Token);
                    _logger.LogInformation("MQTT client subscribed to the root topic");
                }
            }, _cts.Token);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _mqttClient?.Dispose();
        }
    }
}