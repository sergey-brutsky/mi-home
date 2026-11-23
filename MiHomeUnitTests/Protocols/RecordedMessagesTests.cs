using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway.ActionProcessors;
using MiHomeLib.Protocols;
using MiHomeLib.XiaomiGateway2;
using MiHomeUnitTests.Infrastructure;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class RecordedMessagesTests
{
    public static IEnumerable<object[]> Messages()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recorded");
        foreach (var file in Directory.GetFiles(directory, "*.jsonl").OrderBy(path => path))
        foreach (var line in File.ReadLines(file).Where(line => !string.IsNullOrWhiteSpace(line)))
            yield return [line];
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Recorded_Message_Parses_Without_Unknown_Resources_Or_Errors(string line)
    {
        using var record = JsonDocument.Parse(line);
        var root = record.RootElement;
        using var logs = new ErrorRecordingLoggerFactory();
        if (root.GetProperty("gateway").GetString() == "XiaomiGateway2")
        {
            var message = Gateway2Message.Parse(root.GetProperty("message").GetString());
            if (message.Model == "gateway" || message.Type == Gateway2MessageType.GetIdListAck) return;
            if (!ProtocolDeviceRegistry.TryCreate(DeviceProtocol.XiaomiGateway2, message.Model, message.Sid, message.ShortId, logs, out var device)) return;
            using (device) device.ParseData(message.Data);
        }
        else
        {
            if (root.GetProperty("topic").GetString() != "zigbee/send" || !root.TryGetProperty("model", out var model)) return;
            using var message = JsonDocument.Parse(root.GetProperty("message").GetString());
            var cmd = message.RootElement.GetProperty("cmd").GetString();
            if (cmd is not ("report" or "heartbeat" or "read_rsp")) return;
            var did = message.RootElement.GetProperty("did").GetString();
            if (!ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model.GetString(), did, 0, logs, out var device)) return;
            using (device)
            {
                var devices = new GatewayDevices();
                devices.Add(did, device);
                IActionProcessor processor = cmd switch
                {
                    "read_rsp" => new ZigbeeReadResponseCommandProcessor(devices, logs),
                    "heartbeat" => new ZigbeeHeartBeatCommandProcessor(devices, logs),
                    _ => new ZigbeeReportCommandProcessor(devices, logs)
                };
                processor.ProcessMessage(message.RootElement);
                // The real -4003 read_rsp deliberately contains no value; it must not be dispatched.
                if (cmd == "read_rsp" && message.RootElement.GetProperty("results")[0].GetProperty("error_code").GetInt32() != 0)
                    device.LastTimeMessageReceived.Should().BeNull();
            }
        }
        logs.ThrowIfAnyErrors();
        logs.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error ||
            entry.Level == LogLevel.Warning && entry.Message.Contains("is not supported for this device yet"));
    }
}
