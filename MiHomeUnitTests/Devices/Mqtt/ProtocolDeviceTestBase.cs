using MiHomeUnitTests.Infrastructure;
using MiHomeUnitTests.Infrastructure.Messages;
using MiHomeLib.Protocols;
using MiHomeLib.MqttGateway.Discovery;
using MiHomeLib.MqttGateway;
using MiHomeLib.MiioDevices;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using Microsoft.Extensions.Logging;
using MiHomeLib;
using MiHomeLib.Devices;

using Moq;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse.BleAsyncEventParams;

namespace MiHomeUnitTests.Devices.Mqtt;

public abstract class ProtocolDeviceTestBase: MiioDeviceTestBase, IDisposable
{
    protected readonly ErrorRecordingLoggerFactory _loggerFactory = new();
    protected readonly Fixture _fixture = new();
    private readonly List<GatewaySubDeviceBase> _createdDevices = [];
    private readonly JsonSerializerOptions _opts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    protected ProtocolDeviceTestBase()
    {
        _fixture.Customize<ILoggerFactory>(x => x.FromFactory(() => _loggerFactory));

    }
    protected T CreateLumiProtocolDevice<T>(string model) where T : GatewaySubDeviceBase
    {
        if (!ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, _fixture.Create<string>(), 0, _loggerFactory, out var device))
            throw new InvalidOperationException($"No protocol device is registered for model '{model}'.");

        _createdDevices.Add(device);
        return (T)device;
    }

    protected T CreateLumiBleProtocolDevice<T>(int pdid) where T : GatewaySubDeviceBase
    {
        if (!ProtocolDeviceRegistry.TryCreateBle(pdid, _fixture.Create<string>(), _loggerFactory, out var device))
            throw new InvalidOperationException($"No BLE protocol device is registered for pdid '{pdid}'.");

        _createdDevices.Add(device);
        return (T)device;
    }


    public virtual void Dispose()
    {
        var failures = new List<Exception>();
        foreach (var device in _createdDevices)
        {
            try { device.Dispose(); }
            catch (Exception failure) { failures.Add(failure); }
        }
        _createdDevices.Clear();
        failures.AddRange(_loggerFactory.TakeErrors());
        if (failures.Count > 0) throw new AggregateException("Device cleanup or logged operations failed", failures);
    }

    protected List<ZigbeeReportResponse.ZigbeeReportResource> DataToZigbeeResource(string data)
    {
        return JsonSerializer.Deserialize<List<ZigbeeReportResponse.ZigbeeReportResource>>(data, _opts);
    }
    protected static T GetMiSpecValue<T>(string data) => (JsonNode.Parse(data) as JsonArray)[0]["value"].GetValue<T>();
    protected BleAsyncEventParams SetupBleAsyncEventParams(int eid, string edata, double time = 1_700_000_000)
    {
        return _fixture
                    .Build<BleAsyncEventParams>()
                    .With(x => x.Dev,
                        _fixture
                            .Build<BleAsyncEventDevice>()
                            .Create()
                    )
                    .With(x => x.Evt, [new BleAsyncEventEvt() { Eid = eid, Edata = edata }])
                    .With(x => x.Gwts, time)
                    .Create();
    }
}
