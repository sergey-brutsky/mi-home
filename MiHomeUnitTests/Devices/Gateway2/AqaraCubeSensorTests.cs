using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Xunit;
using static MiHomeLib.Devices.AqaraCubeSensor;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraCubeSensorTests : Gateway2DeviceTestBase
{
    private const string Sid = "158d0002c8e2b1";
    private readonly AqaraCubeSensor _cube;

    public AqaraCubeSensorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL, Sid, 4321, _loggerFactory, out var device));
        _cube = Assert.IsType<AqaraCubeSensor>(device);
    }

    [Theory]
    [InlineData("move", CubeStatus.Move)]
    [InlineData("flip90", CubeStatus.Flip90)]
    [InlineData("flip180", CubeStatus.Flip180)]
    [InlineData("tap_twice", CubeStatus.TapTwice)]
    [InlineData("shake_air", CubeStatus.ShakeAir)]
    [InlineData("swing", CubeStatus.Swing)]
    [InlineData("alert", CubeStatus.Alert)]
    [InlineData("free_fall", CubeStatus.FreeFall)]
    public void Status_Raises_OnStatusChanged(string status, CubeStatus expected)
    {
        var raised = new List<CubeStatus>();
        _cube.OnStatusChangedAsync += x => { raised.Add(x); return Task.CompletedTask; };

        _cube.ParseData($"{{\"status\":\"{status}\"}}");

        raised.Should().Equal(expected);
        _cube.Status.Should().Be(expected);
    }

    [Fact]
    public void Same_Action_Twice_Raises_Event_Twice()
    {
        var raised = 0;
        _cube.OnStatusChangedAsync += _ => { raised++; return Task.CompletedTask; };

        _cube.ParseData("{\"status\":\"tap_twice\"}");
        _cube.ParseData("{\"status\":\"tap_twice\"}");

        raised.Should().Be(2);
    }

    [Fact]
    public void Rotate_Sets_Rotation_And_Raises_Rotated()
    {
        CubeStatus? raised = null;
        _cube.OnStatusChangedAsync += x => { raised = x; return Task.CompletedTask; };

        _cube.ParseData("{\"rotate\":\"-90,500\"}");

        raised.Should().Be(CubeStatus.Rotated);
        _cube.Rotate.Shift.Should().Be(-90);
        _cube.Rotate.Value.Should().Be(500);
    }

    [Fact]
    public void Voltage_And_ToString()
    {
        _cube.ParseData("{\"voltage\":3015}");

        _cube.Voltage.Should().Be(3.015f);
        _cube.ToString().Should().Be($"Device: MFKZQ01LM sensor_cube.aqgl01 {Sid}, Voltage: {3.015f}V, Rotate: [0,0], Status: Undefined");
    }

    [Fact]
    public void Unknown_Action_Is_Logged_And_Reported_As_Undefined()
    {
        CubeStatus? raised = null;
        _cube.OnStatusChangedAsync += x => { raised = x; return Task.CompletedTask; };

        _cube.ParseData("{\"status\":\"something_new\"}");

        raised.Should().Be(CubeStatus.Undefined);
        _loggerFactory.Entries.Should().Contain(x => x.Message.Contains("something_new"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("17")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("\"\"")]
    [InlineData("\"90\"")]
    [InlineData("\"90,500,1\"")]
    [InlineData("\"bad,500\"")]
    [InlineData("\"90,bad\"")]
    [InlineData("\"32768,500\"")]
    [InlineData("\"90,-32769\"")]
    [InlineData("\"1.5,500\"")]
    public void Invalid_Rotation_Preserves_State_And_Does_Not_Prevent_Following_Reports(string rawValue)
    {
        _cube.ParseData("{\"rotate\":\"-90,500\"}");
        var events = new List<CubeStatus>();
        _cube.OnStatusChangedAsync += status => { events.Add(status); return Task.CompletedTask; };

        _cube.ParseData($"{{\"rotate\":{rawValue},\"voltage\":2995}}");

        _cube.Rotate.Shift.Should().Be(-90);
        _cube.Rotate.Value.Should().Be(500);
        _cube.Status.Should().Be(CubeStatus.Rotated);
        events.Should().BeEmpty();
        _cube.Voltage.Should().Be(2.995f);
        _loggerFactory.Entries.Should().ContainSingle(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            entry.OriginalFormat == "Rotation '{Value}' is not 'shift,value'");

        _cube.ParseData("{\"rotate\":\"45,250\"}");
        _cube.Rotate.Shift.Should().Be(45);
        _cube.Rotate.Value.Should().Be(250);
        events.Should().Equal(CubeStatus.Rotated);
    }

    [Fact]
    public void Rotation_Accepts_Signed_Short_Boundaries()
    {
        _cube.ParseData("{\"rotate\":\"-32768,32767\"}");
        _cube.Rotate.Shift.Should().Be(short.MinValue);
        _cube.Rotate.Value.Should().Be(short.MaxValue);
        _cube.Status.Should().Be(CubeStatus.Rotated);
    }
}
