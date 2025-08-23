using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using System;
using System.Collections.Generic;
using System.Linq;
using static MiHomeLib.MultimodeGateway.JsonResponses.ZigbeeHearbeatResponse;
using static MiHomeLib.MultimodeGateway.JsonResponses.ZigbeeHearbeatResponse.ZigbeeHearbeatItem;
using static MiHomeLib.MultimodeGateway.JsonResponses.BleAsyncEventResponse;
using static MiHomeLib.MultimodeGateway.JsonResponses.BleAsyncEventResponse.BleAsyncEventParams;
using MiHomeLib.Transport;
using System.Threading.Tasks;
using MiHomeLib;
using MiHomeLib.MultimodeGateway;
using MiHomeLib.MultimodeGateway.JsonResponses;
using MiHomeLib.MultimodeGateway.Devices;
using MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

namespace MiHomeUnitTests;

public class XiaomiMultimodeGatewayTests: MultimodeGatewayDeviceTests
{
    private readonly Mock<IMiioTransport> _miioTransport;
    private readonly Mock<IDevicesDiscoverer> _devicesDiscoverer;
    private readonly MultimodeGateway _gateway; 
    public XiaomiMultimodeGatewayTests()
    {
        _miioTransport = new Mock<IMiioTransport>();
        _devicesDiscoverer = new Mock<IDevicesDiscoverer>();
        
        _devicesDiscoverer
            .Setup(x => x.DiscoverZigBeeDevices())
            .Returns([]);

        _devicesDiscoverer
            .Setup(x => x.DiscoverBleDevices())
            .Returns([]);

        _gateway = new MultimodeGateway(_miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object);
    }
    private void SetupZigBeeDevices(List<(string did, string model, int[] props)> devices)
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
                .Setup(x => x.SendMessageRepeated(It.Is<string>(s => s.Contains("get_device_prop") && s.Contains(did)), It.IsAny<int>()))
                .Returns(getDevicePropResponse.ToString());
        }

        _devicesDiscoverer
            .Setup(x => x.DiscoverZigBeeDevices())
            .Returns(devices.Select(x => (x.did, x.model)).ToList());
    }
    private void SetupZigBeeMiSpecDevices(List<(string did, string model)> devices)
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
                .Setup(x => x.SendMessageRepeated(It.Is<string>(s => s.Contains("get_device_prop") && s.Contains(did)), It.IsAny<int>()))
                .Returns(getDevicePropResponse.ToString());
        }

        _devicesDiscoverer
            .Setup(x => x.DiscoverZigBeeDevices())
            .Returns(devices.Select(x => (x.did, x.model)).ToList());
    }
    private void SetupBleDevices(List<(string did, int pdid, string mac)> devices)
    {
        _devicesDiscoverer
            .Setup(x => x.DiscoverBleDevices())
            .Returns(devices);
    }
    private void RaiseZigbeeReportEvent(string did, double time, List<(string res, int value)> props)
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
    private void RaiseZigbeeMiSpecReportEvent(string did, double time, List<(int siid, int piid, object val)> miSpec)
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
    private void RaiseZigbeeHeartbeatEvent(string did, double time, List<(string res, int value)> props)
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
    private void RaiseBleAsyncEvent(int pdid, string did, string mac, double time, List<(int eid, string edata)> data)
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
                    .With(x => x.Evt, data.Select(x => new BleAsyncEventEvt(){ Eid = x.eid, Edata = x.edata }).ToList())
                    .With(x => x.Gwts, time)
                    .Create()
            )
            .Create();   

        _mqttTransport.Raise(x => x.OnMessageReceived += null, "miio/report", asyncEventResponse.ToString());
    }
    [Fact]
    public void OnDeviceDiscovered_Works()
    {
        // Arrange
        var eventRaised = false;
        var switchDid = _fixture.Create<string>();
        var thDid = _fixture.Create<string>();
        var mac = _fixture.Create<string>()[..12];
        
        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.MODEL, [3032, 100, 192, 88]),
        ]);

        SetupBleDevices([
            (thDid, XiaomiBluetoothHygrothermograph2.PDID, mac),
        ]);

        var counter = 0;

        _gateway.OnDeviceDiscoveredAsync += device => 
        {
            counter++;
            eventRaised = true;

            if(device is XiaomiSmartWirelessSwitch sw) sw.Did.Should().Be(switchDid);
            if(device is XiaomiBluetoothHygrothermograph2 th) th.Did.Should().Be(thDid);

            return Task.CompletedTask;
        };        
        
        // Act    
        _gateway.DiscoverDevices();
        
        // Assert
        eventRaised.Should().BeTrue();
        counter.Should().Be(2);
    } 
    [Fact]
    public void ZigbeeReportCommand_Works()
    {
        // Arrange
        var eventRaised = false;
        var did = _fixture.Create<string>();
        var time = (double)DateTimeOffset.Now.ToUnixTimeMilliseconds();

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.MODEL, [3032, 100, 192, 88]),
        ]);

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.Did.Should().Be(did);
            var sw = device as XiaomiSmartWirelessSwitch;

            sw.OnClickAsync += clickArgs =>
            {
                eventRaised = clickArgs == XiaomiSmartWirelessSwitch.ClickArg.SingleClick;
                sw.LastTimeMessageReceived.Should().Be(time.UnixMilliSecondsToDateTime());
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act    
        _gateway.DiscoverDevices();
        
        RaiseZigbeeReportEvent(did, time,[("13.1.85", 1)]);

        // Assert
        eventRaised.Should().BeTrue();
    }
    [Fact]
    public void ZigbeeReportCommandWithMiSpec_Works()
    {
        // Arrange
        var eventRaised = false;
        var did = _fixture.Create<string>();
        var time = (double)DateTimeOffset.Now.ToUnixTimeMilliseconds();

        SetupZigBeeMiSpecDevices([
            (did, AqaraT1WithNeutral.MODEL),
        ]);

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.Did.Should().Be(did);
            
            var aqaraRelay = device as AqaraT1WithNeutral;

            aqaraRelay.OnLoadPowerChangeAsync += x =>
            {
                eventRaised = true;
                aqaraRelay.LastTimeMessageReceived.Should().Be(time.UnixMilliSecondsToDateTime());
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act    
        _gateway.DiscoverDevices();
        
        RaiseZigbeeMiSpecReportEvent(did, time,[(3, 2, 42.2)]);

        // Assert
        eventRaised.Should().BeTrue();
    }
    [Fact]
    public void ZigbeeHeartbeatCommand_Works()
    {
        // Arrange
        var voltageEventRaised = false;
        var batteryEventRaised = false;
        var oldVoltage = 3032;
        var newVoltage = 3012;
        var oldBatteryPerent = 100;
        var newBatteryPerent = 99;
        var did = _fixture.Create<string>();
        var time = (double)DateTimeOffset.Now.ToUnixTimeMilliseconds();

        SetupZigBeeDevices([
            (did, XiaomiSmartWirelessSwitch.MODEL, [oldVoltage, oldBatteryPerent, 192, 88]),
        ]);

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.Did.Should().Be(did);

            var sw = device as XiaomiSmartWirelessSwitch;
            
            sw.OnVoltageChangeAsync += oldValue =>
            {
                voltageEventRaised = true;
                (oldVoltage/1000f).Should().Be(oldValue);
                sw.Voltage.Should().Be(newVoltage/1000f);
                sw.LastTimeMessageReceived.Should().Be(time.UnixMilliSecondsToDateTime());
                return Task.CompletedTask;
            };

            sw.OnBatteryPercentChange += oldValue =>
            {
                batteryEventRaised = true;
                oldBatteryPerent.Should().Be(oldBatteryPerent);
                sw.BatteryPercent.Should().Be((byte)newBatteryPerent);
                sw.LastTimeMessageReceived.Should().Be(time.UnixMilliSecondsToDateTime());
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        };

        // Act    
        _gateway.DiscoverDevices();
        
        RaiseZigbeeHeartbeatEvent(did, time,[("8.0.2008", newVoltage), ("8.0.2001", newBatteryPerent)]);

        // Assert
        voltageEventRaised.Should().BeTrue();
        batteryEventRaised.Should().BeTrue();
    }
    [Fact]
    public void AsyncBleEventMethod_Works()
    {
        // Arrange
        var temperatureEventRaised = false;
        var did = _fixture.Create<string>();
        var mac = _gateway.DecodeMacAddress(_fixture.Create<string>()[..12]);
        
        double time = DateTimeOffset.Now.ToUnixTimeSeconds();

        SetupBleDevices([
            (did, XiaomiBluetoothHygrothermograph2.PDID, mac),
        ]);

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            device.Did.Should().Be(did);

            var th = device as XiaomiBluetoothHygrothermograph2;
            
            th.OnTemperatureChangeAsync += oldValue =>
            {
                temperatureEventRaised = true;
                th.LastTimeMessageReceived.Should().Be(time.UnixSecondsToDateTime());
                return Task.CompletedTask;
            };
            
            return Task.CompletedTask;
        };

        // Act    
        _gateway.DiscoverDevices();
        
        RaiseBleAsyncEvent(XiaomiBluetoothHygrothermograph2.PDID, did, mac, time, [(4100, "e500")]);

        // Assert
        temperatureEventRaised.Should().BeTrue();
    }
    
    [Fact]
    public void GetDevices_When_Any_Returns_DiscoveredDevicesList()
    {
        // Arrange
        var switchDid = _fixture.Create<string>();
        var thSensorDid = _fixture.Create<string>();
        var thMonitorDid = _fixture.Create<string>();
        var mac = _fixture.Create<string>()[..12];
        
        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.MODEL, [3032, 100, 192, 88]),
            (thSensorDid, XiaomiTemperatureHumiditySensor.MODEL, [3032, 100, 192, 88]),
        ]);

        SetupBleDevices([
            (thMonitorDid, XiaomiBluetoothHygrothermograph2.PDID, mac),
        ]);

        // Act    
        _gateway.DiscoverDevices();
        var devices = _gateway.GetDevices();
        
        // Assert
        devices.Count.Should().Be(3);
        devices.Any(x => x.Did == switchDid && x.GetType() == typeof(XiaomiSmartWirelessSwitch)).Should().BeTrue();
        devices.Any(x => x.Did == thSensorDid && x.GetType() == typeof(XiaomiTemperatureHumiditySensor)).Should().BeTrue();
        devices.Any(x => x.Did == thMonitorDid && x.GetType() == typeof(XiaomiBluetoothHygrothermograph2)).Should().BeTrue();
    }
    
    [Fact]
    public void GetDevices_When_NoOne_Returns_EmptyList()
    {
        // Arrange

        // Act    
        _gateway.DiscoverDevices();
        
        // Assert
        _gateway.GetDevices().Count.Should().Be(0);
    }

    [Fact]
    public void GetDeviceByDid_When_NotFound_Returns_Null()
    {
        // Arrange
        var did = _fixture.Create<string>();

        // Act    
        _gateway.DiscoverDevices();
        
        // Assert
        _gateway.GetDeviceByDid<XiaomiSmartWirelessSwitch>(did).Should().BeNull();
    }

    [Fact]
    public void GetDeviceByDid_WhenExistsAndHasCorrectType_Returns_Device()
    {
        // Arrange
        var switchDid = _fixture.Create<string>();
        
        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.MODEL, [3032, 100, 192, 88]),
            (_fixture.Create<string>(), XiaomiTemperatureHumiditySensor.MODEL, [3032, 100, 192, 88]),
        ]);

        SetupBleDevices([
            (_fixture.Create<string>(), XiaomiBluetoothHygrothermograph2.PDID, _fixture.Create<string>()),
        ]);

        // Act    
        _gateway.DiscoverDevices();
        
        // Assert
        _gateway
            .GetDeviceByDid<XiaomiSmartWirelessSwitch>(switchDid)
                .Should()
                .NotBeNull()
                .And
                .Match<XiaomiSmartWirelessSwitch>(x => x.Did == switchDid);
    }

    [Fact]
    public void GetDeviceByDid_WhenHasWrongType_Returns_Null()
    {
        // Arrange
        var switchDid = _fixture.Create<string>();
        
        SetupZigBeeDevices([
            (switchDid, XiaomiSmartWirelessSwitch.MODEL, [3032, 100, 192, 88]),
            (_fixture.Create<string>(), XiaomiTemperatureHumiditySensor.MODEL, [3032, 100, 192, 88]),
        ]);

        SetupBleDevices([
            (_fixture.Create<string>(), XiaomiBluetoothHygrothermograph2.PDID, _fixture.Create<string>()),
        ]);

        // Act    
        _gateway.DiscoverDevices();
        
        // Assert
        _gateway.GetDeviceByDid<XiaomiTemperatureHumiditySensor>(switchDid).Should().BeNull();
    }   
}
