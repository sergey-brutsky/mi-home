using MiHomeUnitTests.Devices.Mqtt;
using MiHomeUnitTests.Infrastructure.Messages;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway.Discovery;
using MiHomeLib.Helpers;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;
using AutoFixture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MiHomeLib;
using MiHomeLib.MqttGateway;
using AqaraT1WithNeutral = MiHomeLib.Devices.AqaraT1WithNeutral;
using XiaomiBluetoothHygrothermograph3 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph3;
using XiaomiMeshWallSwitchSingleOpen = MiHomeLib.Devices.XiaomiMeshWallSwitchSingleOpen;
using UnifiedTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;
using UnifiedAqaraTemperatureHumiditySensor = MiHomeLib.Devices.AqaraTemperatureHumiditySensor;
using XiaomiSmartWirelessSwitch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;
using XiaomiBluetoothHygrothermograph2 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph2;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse.BleAsyncEventParams;
using static MiHomeUnitTests.Infrastructure.Messages.ZigbeeHearbeatResponse;
using static MiHomeUnitTests.Infrastructure.Messages.ZigbeeHearbeatResponse.ZigbeeHearbeatItem;

namespace MiHomeUnitTests.Gateways.Mqtt;

public abstract class MqttGatewayTestBase : MqttDeviceTestBase
{
    protected readonly string _gwDid = "397569381";
    protected readonly Mock<MqttGatewayBase> _gw;
    private MqttGatewayBase _createdGateway;
    protected MqttGatewayBase Gateway => _createdGateway ??= _gw.Object;

    protected MqttGatewayTestBase()
    {
        _devicesDiscoverer.Setup(x => x.DiscoverZigBeeDevices()).Returns([]);
        _devicesDiscoverer.Setup(x => x.DiscoverBleDevices()).Returns([]);
        _devicesDiscoverer.Setup(x => x.DiscoverMeshDevices()).Returns([]);
        _gw = new Mock<MqttGatewayBase>(_gwDid, _miioTransport.Object, _mqttTransport.Object,
            _devicesDiscoverer.Object, _loggerFactory) { CallBase = true };
    }

    public override void Dispose()
    {
        _createdGateway?.Dispose();
        base.Dispose();
    }

    protected void SetupZigBeeDevices(List<(string did, string model, int[] props)> devices)
    {
        foreach (var (did, model, props) in devices)
        {
            var getDevicePropResponse = _fixture
                .Build<GetDevicePropResponse>()
                .With(x => x.Code, 0)
                .With(x => x.Result, props)
                .Without(x => x.Error)
                .Create();

            _miioTransport
                .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_device_prop") && s.Contains(did))))
                .ReturnsAsync(getDevicePropResponse.ToString());
        }

        _devicesDiscoverer
            .Setup(x => x.DiscoverZigBeeDevices())
            .Returns(devices.Select(x => new ZigBeeDeviceInfo(x.did, x.model)).ToList());
    }
    protected void SetupZigBeeMiSpecDevices(List<(string did, string model)> devices)
    {
        foreach (var (did, model) in devices)
        {
            var getDevicePropResponse = _fixture
                .Build<GetDevicePropResponse>()
                .Without(x => x.Code)
                .Without(x => x.Result)
                .With(x => x.Error, new GetDevicePropResponse.MiioError { Code = -5015, Message = "device not found" })
                .Create();

            _miioTransport
                .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_device_prop") && s.Contains(did))))
                .ReturnsAsync(getDevicePropResponse.ToString());
        }

        _devicesDiscoverer
            .Setup(x => x.DiscoverZigBeeDevices())
            .Returns(devices.Select(x => new ZigBeeDeviceInfo(x.did, x.model)).ToList());
    }
    protected void SetupBleDevices(List<(string did, int pdid, string mac)> devices)
    {
        _devicesDiscoverer
            .Setup(x => x.DiscoverBleDevices())
            .Returns(devices.Select(x => new BleDeviceInfo(x.did, x.pdid, x.mac)).ToList());
    }
    protected void RaiseZigbeeReportEvent(string did, double time, List<(string res, int value)> props)
    {
        var zigbeeReport = _fixture
            .Build<ZigbeeReportResponse>()
            .With(x => x.Did, did)
            .With(x => x.Time, time)
            .Without(x => x.MiSpec)
            .With(x => x.Params,
                props.Select(x => new ZigbeeReportResponse.ZigbeeReportResource
                {
                    ResName = x.res,
                    Value = x.value,
                }).ToList())
            .Create();

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "zigbee/send", zigbeeReport.ToString());
    }
    protected void RaiseZigbeeMiSpecReportEvent(string did, double time, List<(int siid, int piid, object val)> miSpec)
    {
        var zigbeeReport = _fixture
            .Build<ZigbeeReportResponse>()
            .With(x => x.Did, did)
            .With(x => x.Time, time)
            .Without(x => x.Params)
            .With(x => x.MiSpec,
                miSpec.Select(x => new ZigbeeReportResponse.ZigbeeMiSpecItem
                {
                    Siid = x.siid,
                    Piid = x.piid,
                    Value = x.val,
                }).ToList())
            .Create();

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "zigbee/send", zigbeeReport.ToString());
    }
    protected void RaiseZigbeeHeartbeatEvent(string did, double time, List<(string res, int value)> props)
    {
        var @params = new List<ZigbeeHearbeatItem>()
        {
            new() {
                Did = did,
                Time = time,
                Zseq = _fixture.Create<byte>(),
                ResList = props.Select(x => new ZigbeeHearbeatItemResource() { ResName = x.res, Value = x.value }).ToList(),
            }
        };

        var zigbeeHeartBeat = _fixture
            .Build<ZigbeeHearbeatResponse>()
            .With(x => x.Time, time)
            .With(x => x.Params, @params)
            .Create();

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "zigbee/send", zigbeeHeartBeat.ToString());
    }
    protected void RaiseBleAsyncEvent(int pdid, string did, string mac, double time, List<(int eid, string edata)> data)
    {
        var asyncEventResponse = _fixture
               .Build<BleAsyncEventResponse>()
               .With(x => x.Params,
                   _fixture
                       .Build<BleAsyncEventParams>()
                       .With(x => x.Dev,
                           _fixture
                               .Build<BleAsyncEventDevice>()
                               .With(x => x.Did, did)
                               .With(x => x.Pdid, pdid)
                               .With(x => x.Mac, mac)
                               .Create()
                       )
                       .With(x => x.Evt, data.Select(x => new BleAsyncEventEvt() { Eid = x.eid, Edata = x.edata }).ToList())
                       .With(x => x.Gwts, time)
                       .Create()
               )
               .Create();

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "central/report", asyncEventResponse.ToString());
    }

}
