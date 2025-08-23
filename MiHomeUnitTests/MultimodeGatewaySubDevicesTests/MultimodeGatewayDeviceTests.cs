using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MultimodeGateway.JsonResponses;
using MiHomeLib.Transport;
using Moq;
using static MiHomeLib.MultimodeGateway.JsonResponses.BleAsyncEventResponse;
using static MiHomeLib.MultimodeGateway.JsonResponses.BleAsyncEventResponse.BleAsyncEventParams;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class MultimodeGatewayDeviceTests
{
    protected readonly Mock<IMqttTransport> _mqttTransport;
    protected readonly NullLoggerFactory _loggerFactory;
    protected readonly Fixture _fixture = new();
    private readonly JsonSerializerOptions _opts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public MultimodeGatewayDeviceTests()
    {
        _fixture.Customize<ILoggerFactory>(x => x.FromFactory(() => new NullLoggerFactory()));
        _mqttTransport = new Mock<IMqttTransport>();
        _loggerFactory = new NullLoggerFactory();
    }

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
