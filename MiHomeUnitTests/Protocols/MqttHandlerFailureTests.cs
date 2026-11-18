using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using MiHomeUnitTests.Gateways.Mqtt;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class MqttHandlerFailureTests : MqttGatewayTestBase
{
    public static IEnumerable<object[]> Messages => MqttMessageProcessorTests.Messages.SelectMany(message =>
        new[] { "invalid-operation", "argument", "format", "missing-key", "null-reference" }
            .Select(kind => new object[] { message[0], message[1], kind }));

    [Theory]
    [MemberData(nameof(Messages))]
    public void Handler_Failure_Is_Not_Reported_As_Invalid_Mqtt_Data(string action, string json, string kind)
    {
        Exception failure = kind switch
        {
            "invalid-operation" => new InvalidOperationException("handler defect"),
            "argument" => new ArgumentException("handler defect"),
            "format" => new FormatException("handler defect"),
            "missing-key" => new KeyNotFoundException("handler defect"),
            _ => new NullReferenceException("handler defect")
        };
        var device = new FailingDevice(failure);
        var devices = (GatewayDevices)typeof(MqttGatewayBase).GetField("_devices",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Gateway);
        devices.Add(device.DeviceId, device);
        Gateway.DiscoverDevices();
        var topic = action is "report" or "read_rsp" or "heartbeat" ? "zigbee/send" : "central/report";
        var field = topic == "zigbee/send" ? "cmd" : "method";
        var payload = "{\"" + field + "\":\"" + action + "\"," + json[1..];
        Action receive = () => _mqttTransport.Raise(transport => transport.OnMessageReceived += null, topic, payload);

        receive.Should().Throw<Exception>().Which.Should().BeSameAs(failure);
        _loggerFactory.Entries.Should().NotContain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Invalid MQTT data"));

        device.Failure = null;
        receive.Should().NotThrow();
        device.SuccessfulMessages.Should().Be(1);
    }

    private sealed class FailingDevice(Exception failure) : GatewaySubDeviceBase(NullLoggerFactory.Instance)
    {
        public override string DeviceId => "known";
        public Exception Failure { get; set; } = failure;
        public int SuccessfulMessages { get; private set; }
        internal override void ParseData(string command)
        {
            if (Failure is not null) throw Failure;
            SuccessfulMessages++;
        }
    }
}
