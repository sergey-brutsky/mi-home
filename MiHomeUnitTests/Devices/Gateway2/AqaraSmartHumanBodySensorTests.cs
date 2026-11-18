using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraSmartHumanBodySensorTests : Gateway2DeviceTestBase
{
    private const string Sid = "158d0001d45e2b";
    private const int ShortId = 4321;
    private readonly AqaraSmartHumanBodySensor _device;

    public AqaraSmartHumanBodySensorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, AqaraSmartHumanBodySensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId, _loggerFactory, out var device));
        _device = Assert.IsType<AqaraSmartHumanBodySensor>(device);
    }

    private void Parse(string cmd, Dictionary<string, object> data) =>
        _device.ParseData(Gateway2Message.Parse(
            CreateCommand(cmd, AqaraSmartHumanBodySensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId, data)).Data);

    [Fact]
    public void ReadAck_Sets_Voltage_And_Lux()
    {
        Parse("read_ack", new() { { "voltage", 3005 }, { "lux", 87 } });

        _device.DeviceId.Should().Be(Sid);
        _device.ShortId.Should().Be(ShortId);
        _device.Voltage.Should().Be(3.005f);
        _device.Lux.Should().Be(87);
        _device.ToString().Should().Be($"Device: RTCGQ11LM sensor_motion.aq2 {Sid}, Voltage: {3.005f}V, Lux: 87");
    }

    [Fact]
    public void Lux_Accepts_String_And_Passes_Old_Value()
    {
        Parse("report", new() { { "lux", 10 } });
        int? oldLux = null;
        _device.OnLuxChangeAsync += x => { oldLux = x; return Task.CompletedTask; };

        Parse("report", new() { { "lux", "250" } });

        _device.Lux.Should().Be(250);
        oldLux.Should().Be(10);
    }

    [Fact]
    public void Motion_Raises_OnMotion_And_Sets_MotionDate()
    {
        var raised = 0;
        _device.OnMotionAsync += () => { raised++; return Task.CompletedTask; };

        Parse("report", new() { { "status", "motion" }, { "lux", 40 } });

        raised.Should().Be(1);
        _device.MotionDate.Should().NotBeNull();
        _device.Lux.Should().Be(40);
    }

    [Theory]
    [InlineData(120)]
    [InlineData("1200")]
    public void NoMotion_Is_Passed_As_Reported(object period)
    {
        var reported = new List<int>();
        _device.OnNoMotionAsync += x => { reported.Add(x); return Task.CompletedTask; };

        Parse("report", new() { { "no_motion", period } });

        reported.Should().Equal(int.Parse(period.ToString()));
    }
}
