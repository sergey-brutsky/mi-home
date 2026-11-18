using MiHomeUnitTests.Infrastructure;
using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MiHomeLib;
using MiHomeLib.MqttGateway.ActionProcessors;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class MqttMessageProcessorTests
{
    [Fact]
    public void Reception_Time_Is_Preserved_Through_Delayed_Processing_And_Old_Source_Times()
    {
        var device = new RecordingDevice(_logs);
        device.LastTimeMessageReceived.Should().BeNull();
        device.LastMessageSourceTimeUtc.Should().BeNull();
        _devices.Add("known", device);
        var first = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var second = first.AddSeconds(1);
        MessageReception.Run(first, () => Process("report",
            """{"did":"known","time":1700000001000,"params":[{"res_name":"8.0.2001","value":90}]}"""));
        device.LastTimeMessageReceived.Should().Be(first);
        MessageReception.Run(second, () => Process("report",
            """{"did":"known","time":1700000000000,"params":[{"res_name":"8.0.2001","value":91}]}"""));
        device.LastTimeMessageReceived.Should().Be(second);
        device.LastMessageSourceTimeUtc.Should().Be(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc));
        MessageReception.Run(second.AddSeconds(1), () => Process("properties_changed",
            """{"params":[{"did":"known","siid":2,"piid":1,"value":true}]}"""));
        device.LastMessageSourceTimeUtc.Should().BeNull();
        MessageReception.UtcNow.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    private readonly ErrorRecordingLoggerFactory _logs = new();
    private readonly GatewayDevices _devices = new();

    public static TheoryData<string, string, string, bool> Messages => new()
    {
        { "report", """{"did":"known","time":1700000000123,"params":[{"res_name":"8.0.2001","value":90}]}""",
            """[{"res_name":"8.0.2001","value":90}]""", false },
        { "report", """{"did":"known","time":1700000000123,"params":[{"res_name":"ignored","value":0}],"mi_spec":[{"siid":2,"piid":1,"value":true}]}""",
            """[{"siid":2,"piid":1,"value":true}]""", false },
        { "heartbeat", """{"time":1,"params":[{"did":"known","time":1700000000123,"res_list":[{"res_name":"8.0.2001","value":90}]}]}""",
            """[{"res_name":"8.0.2001","value":90}]""", false },
        { "read_rsp", """{"did":"known","time":1700000000123,"results":[{"res_name":"8.0.2001","value":90,"error_code":0},{"res_name":"bad","value":1,"error_code":-4003},{"res_name":"missing"},{"value":1},{"res_name":"8.0.2007","value":120}]}""",
            """[{"res_name":"8.0.2001","value":90},{"res_name":"8.0.2007","value":120}]""", false },
        { "_async.ble_event", """{"params":{"dev":{"did":"known"},"gwts":1700000000.123,"evt":[{"eid":4106,"edata":"5a"}]}}""",
            """{"dev":{"did":"known"},"gwts":1700000000.123,"evt":[{"eid":4106,"edata":"5a"}]}""", false },
        { "event_occured", """{"params":{"did":"known","siid":2,"eiid":1,"arguments":[{"piid":1,"value":42}]}}""",
            """{"did":"known","siid":2,"eiid":1,"arguments":[{"piid":1,"value":42}]}""", true },
        { "properties_changed", """{"params":[{"did":"unknown","siid":2,"piid":1,"value":0},{"did":"known","siid":2,"piid":1,"value":true}]}""",
            """{"did":"known","siid":2,"piid":1,"value":true}""", true },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Wire_Messages_Preserve_Payload_And_Time(string action, string json, string payload, bool localTime)
    {
        var device = new RecordingDevice(_logs);
        _devices.Add("known", device);
        var before = DateTime.UtcNow;

        Process(action, json);

        device.Payloads.Should().ContainSingle();
        var actual = JsonNode.Parse(device.Payloads.Single());
        // read_rsp metadata can stay on the borrowed resource; only res_name/value are device data.
        if (action == "read_rsp")
            foreach (var item in actual.AsArray()) item.AsObject().Remove("error_code");
        JsonNode.DeepEquals(actual, JsonNode.Parse(payload)).Should().BeTrue();
        device.LastTimeMessageReceived.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        if (localTime)
            device.LastMessageSourceTimeUtc.Should().BeNull();
        else
            device.LastMessageSourceTimeUtc.Should().BeCloseTo(1700000000123d.UnixMilliSecondsToDateTime().ToUniversalTime(), TimeSpan.FromMilliseconds(1));
        _logs.ThrowIfAnyErrors();
    }

    public static IEnumerable<object[]> UnknownMessages => Messages.Select(x => new object[] { x[0], x[1] });

    [Theory]
    [MemberData(nameof(UnknownMessages))]
    public void Unknown_Device_Is_Skipped(string action, string json)
    {
        Process(action, json);

        _logs.Entries.Should().Contain(x => x.Level == (action == "read_rsp" ? LogLevel.Debug : LogLevel.Warning));
        _logs.ThrowIfAnyErrors();
    }

    [Theory]
    [InlineData("""{"did":"known","results":[]}""")]
    [InlineData("""{"did":"known","results":[{"res_name":"unsupported","error_code":-4003}]}""")]
    public void Empty_Read_Response_Does_Not_Invoke_Device_Or_Replace_Time(string json)
    {
        var device = new RecordingDevice(_logs) { LastTimeMessageReceived = new DateTime(2020, 1, 1) };
        _devices.Add("known", device);

        Process("read_rsp", json);

        device.Payloads.Should().BeEmpty();
        device.LastTimeMessageReceived.Should().Be(new DateTime(2020, 1, 1));
    }

    [Theory]
    [InlineData("""{"params":[]}""")]
    [InlineData("""{"params":[{},{}]}""")]
    [InlineData("""{"params":[{"time":1,"res_list":[]}]}""")]
    public void Unsupported_Heartbeat_Structure_Is_Skipped(string json)
    {
        Process("heartbeat", json);
        _logs.Entries.Should().Contain(x => x.Level == LogLevel.Warning);
    }

    private void Process(string action, string json)
    {
        using var document = JsonDocument.Parse(json);
        CreateProcessor(action).ProcessMessage(document.RootElement);
    }

    private IActionProcessor CreateProcessor(string action) => action switch
    {
        "report" => new ZigbeeReportCommandProcessor(_devices, _logs),
        "heartbeat" => new ZigbeeHeartBeatCommandProcessor(_devices, _logs),
        "read_rsp" => new ZigbeeReadResponseCommandProcessor(_devices, _logs),
        "_async.ble_event" => new AsyncBleEventMethodProcessor(_devices, _logs),
        "event_occured" => new EventOccuredMethodProcessor(_devices, _logs),
        "properties_changed" => new PropertiesChangedMethodProcessor(_devices, _logs),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private sealed class RecordingDevice(ILoggerFactory logs) : GatewaySubDeviceBase(logs)
    {
        public override string DeviceId => "known";
        public List<string> Payloads { get; } = [];
        internal override void ParseData(string command) => Payloads.Add(command);
    }
}
