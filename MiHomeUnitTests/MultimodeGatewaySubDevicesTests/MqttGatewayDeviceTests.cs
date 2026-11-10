using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using Microsoft.Extensions.Logging;
using MiHomeLib;
using MiHomeLib.Contracts;
using MiHomeLib.MqttGateway.JsonResponses;

using Moq;
using static MiHomeLib.MqttGateway.JsonResponses.BleAsyncEventResponse;
using static MiHomeLib.MqttGateway.JsonResponses.BleAsyncEventResponse.BleAsyncEventParams;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class MqttGatewayDeviceTests: MiioDeviceBase, IDisposable
{
    protected Mock<IDevicesDiscoverer> _devicesDiscoverer;
    protected readonly Mock<IMqttTransport> _mqttTransport;
    protected readonly ErrorRecordingLoggerFactory _loggerFactory = new();
    protected readonly Fixture _fixture = new();
    private readonly JsonSerializerOptions _opts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public MqttGatewayDeviceTests()
    {
        _fixture.Customize<ILoggerFactory>(x => x.FromFactory(() => _loggerFactory));
        _mqttTransport = new Mock<IMqttTransport>();
        _miioTransport = new Mock<IMiioTransport>();
        _devicesDiscoverer = new Mock<IDevicesDiscoverer>();

        SetupMiioInfo();
    }

    public void Dispose() => _loggerFactory.ThrowIfAnyErrors();

    protected List<ZigbeeReportResponse.ZigbeeReportResource> DataToZigbeeResource(string data)
    {
        return JsonSerializer.Deserialize<List<ZigbeeReportResponse.ZigbeeReportResource>>(data, _opts);
    }
    protected static T GetMiSpecValue<T>(string data) => (JsonNode.Parse(data) as JsonArray)[0]["value"].GetValue<T>();
    protected BleAsyncEventParams SetupBleAsyncEventParams(int eid, string edata, double time = -1)
    {
        return _fixture
                    .Build<BleAsyncEventParams>()
                    .With(x => x.Dev,
                        _fixture
                            .Build<BleAsyncEventDevice>()
                            .Create()
                    )
                    .With(x => x.Evt, [new BleAsyncEventEvt() { Eid = eid, Edata = edata }])
                    .With(x => x.Gwts, time == -1 ? DateTimeOffset.Now.ToUnixTimeSeconds() : time)
                    .Create();
    }
}
