using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class XiaomiWindowDoorSensorTests : Gateway2DeviceTestBase
{
    private const string Sid = "158d0001d561e2";
    private const int ShortId = 12345;
    private readonly XiaomiWindowDoorSensor _device;

    public XiaomiWindowDoorSensorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2,
            XiaomiWindowDoorSensor.XIAOMI_GATEWAY2_MODEL,
            Sid,
            ShortId,
            _loggerFactory,
            out var device));
        _device = (XiaomiWindowDoorSensor)device;
    }

    [Theory]
    [InlineData("open", true)]
    [InlineData("close", false)]
    public void Gateway2_Status_Updates_Contact_And_Raises_Event(string status, bool opened)
    {
        var eventRaised = false;
        if (opened)
            _device.OnOpenAsync += () => { eventRaised = true; return Task.CompletedTask; };
        else
            _device.OnCloseAsync += () => { eventRaised = true; return Task.CompletedTask; };

        var command = CreateCommand("report", XiaomiWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["status"] = status });
        _device.ParseData(Gateway2Message.Parse(command).Data);

        Assert.True(eventRaised);
        Assert.Equal(opened
            ? XiaomiWindowDoorSensor.DoorWindowContactState.Opened
            : XiaomiWindowDoorSensor.DoorWindowContactState.Closed, _device.Contact);
        Assert.Equal(status, _device.Status);
    }

    [Fact]
    public void Gateway2_Parses_Voltage()
    {
        var command = CreateCommand("heartbeat", XiaomiWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["voltage"] = 3005 });

        _device.ParseData(Gateway2Message.Parse(command).Data);

        Assert.Equal(3.005f, _device.Voltage);
    }

    [Theory]
    [InlineData("60", 1)]
    [InlineData("300", 5)]
    public void Gateway2_NoClose_Raises_Matching_Event(string seconds, int minutes)
    {
        var eventRaised = false;
        if (minutes == 1)
            _device.NotClosedFor1MinuteAsync += () => { eventRaised = true; return Task.CompletedTask; };
        else
            _device.NotClosedFor5MinutesAsync += () => { eventRaised = true; return Task.CompletedTask; };

        var command = CreateCommand("report", XiaomiWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["no_close"] = seconds });
        _device.ParseData(Gateway2Message.Parse(command).Data);

        Assert.True(eventRaised);
    }

    [Theory]
    [InlineData("open", XiaomiWindowDoorSensor.DoorWindowContactState.Opened)]
    [InlineData("close", XiaomiWindowDoorSensor.DoorWindowContactState.Closed)]
    public void Gateway2_Status_Raises_OnContactChanged_As_Well(string status, XiaomiWindowDoorSensor.DoorWindowContactState expected)
    {
        XiaomiWindowDoorSensor.DoorWindowContactState? contactInHandler = null;
        _device.OnContactChangedAsync += () => { contactInHandler = _device.Contact; return Task.CompletedTask; };

        var command = CreateCommand("report", XiaomiWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["status"] = status });
        _device.ParseData(Gateway2Message.Parse(command).Data);

        Assert.Equal(expected, contactInHandler);
    }

    [Fact]
    public void Gateway2_Never_Starts_Not_Closed_Timers()
    {
        var oneMinuteTimer = new Moq.Mock<MiHomeLib.Devices.Timing.ITimer>();
        var fiveMinutesTimer = new Moq.Mock<MiHomeLib.Devices.Timing.ITimer>();
        var sensor = new XiaomiWindowDoorSensor(Sid, ShortId, _loggerFactory, oneMinuteTimer.Object, fiveMinutesTimer.Object);
        sensor.InitializeProtocol(DeviceProtocol.XiaomiGateway2);

        var command = CreateCommand("report", XiaomiWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["status"] = "open" });
        sensor.ParseData(Gateway2Message.Parse(command).Data);

        // Gateway 2 reports "not closed" itself
        oneMinuteTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Moq.Times.Never);
        fiveMinutesTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Moq.Times.Never);
    }

    [Theory]
    [InlineData("open", "60")]
    [InlineData("close", "300")]
    public void Aqara_Model_Is_Registered_For_Gateway2_And_Handles_Its_Reports(string status, string noClose)
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, AqaraWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId, _loggerFactory, out var device));
        var sensor = Assert.IsType<AqaraWindowDoorSensor>(device);
        var raised = new List<string>();
        sensor.OnOpenAsync += () => { raised.Add("open"); return Task.CompletedTask; };
        sensor.OnCloseAsync += () => { raised.Add("close"); return Task.CompletedTask; };
        sensor.NotClosedFor1MinuteAsync += () => { raised.Add("60"); return Task.CompletedTask; };
        sensor.NotClosedFor5MinutesAsync += () => { raised.Add("300"); return Task.CompletedTask; };

        var command = CreateCommand("report", AqaraWindowDoorSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["status"] = status, ["no_close"] = noClose, ["voltage"] = 3015 });
        sensor.ParseData(Gateway2Message.Parse(command).Data);

        Assert.Equal([status, noClose], raised);
        Assert.Equal(3.015f, sensor.Voltage);
        Assert.Equal($"Device: MCCGQ11LM sensor_magnet.aq2 {Sid}, Voltage: {3.015f}V, Status: {status}", sensor.ToString());
    }
}
