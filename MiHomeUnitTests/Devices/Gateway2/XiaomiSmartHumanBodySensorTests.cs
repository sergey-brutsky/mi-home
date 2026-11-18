using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class XiaomiSmartHumanBodySensorTests : Gateway2DeviceTestBase
{
    private readonly string _sid = "158d0001214e1f";
    private readonly int _shortId = 12345;
    private readonly XiaomiSmartHumanBodySensor _device;

    public XiaomiSmartHumanBodySensorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2,
            XiaomiSmartHumanBodySensor.XIAOMI_GATEWAY2_MODEL,
            _sid,
            _shortId,
            _loggerFactory,
            out var device));
        _device = (XiaomiSmartHumanBodySensor)device;
    }

    private void Parse(string cmd, Dictionary<string, object> data) =>
        _device.ParseData(Gateway2Message.Parse(
            CreateCommand(cmd, XiaomiSmartHumanBodySensor.XIAOMI_GATEWAY2_MODEL, _sid, _shortId, data)).Data);

    [Fact]
    public void Check_MotionSensor_ReadAck_Data()
    {
        Parse("read_ack", new() { { "voltage", 2985 } });

        _device.DeviceId.Should().Be(_sid);
        _device.ShortId.Should().Be(_shortId);
        _device.Voltage.Should().Be(2.985f);
        _device.ToString().Should().Be($"Device: RTCGQ01LM motion {_sid}, Voltage: 2.985V".Replace("2.985", 2.985f.ToString()));
    }

    [Theory]
    [InlineData("60")]
    [InlineData("120")]
    [InlineData("180")]
    [InlineData("300")]
    [InlineData("600")]
    [InlineData("1800")]
    public void Check_MotionSensor_Reports_NoMotion_Data(string period)
    {
        var reported = new List<int>();
        _device.OnNoMotionAsync += seconds => { reported.Add(seconds); return Task.CompletedTask; };

        Parse("report", new() { { "no_motion", period } });

        reported.Should().Equal(int.Parse(period));
    }

    [Fact]
    public void Check_MotionSensor_Reports_Motion_Data()
    {
        var motionRaised = 0;
        _device.OnMotionAsync += () => { motionRaised++; return Task.CompletedTask; };

        Parse("report", new() { { "status", "motion" } });

        motionRaised.Should().Be(1);
    }
}
