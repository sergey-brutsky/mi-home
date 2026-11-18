using MiHomeLib.Protocols;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraVibrationSensorTests : Gateway2DeviceTestBase
{
    private readonly AqaraVibrationSensor _device;

    public AqaraVibrationSensorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, AqaraVibrationSensor.XIAOMI_GATEWAY2_MODEL, "158d0002a12345", 1234, _loggerFactory, out var device));
        _device = Assert.IsType<AqaraVibrationSensor>(device);
    }

    [Theory]
    [InlineData("vibrate", "vibration")]
    [InlineData("tilt", "tilt")]
    [InlineData("free_fall", "free_fall")]
    public void Status_Raises_Matching_Event(string status, string expected)
    {
        var raised = new List<string>();
        _device.OnVibrationAsync += () => { raised.Add("vibration"); return Task.CompletedTask; };
        _device.OnTiltAsync += () => { raised.Add("tilt"); return Task.CompletedTask; };
        _device.OnFreeFallAsync += () => { raised.Add("free_fall"); return Task.CompletedTask; };

        _device.ParseData($"{{\"status\":\"{status}\"}}");

        raised.Should().Equal(expected);
    }

    [Fact]
    public void FinalTiltAngle_Passes_Old_Value()
    {
        _device.ParseData("{\"final_tilt_angle\":\"30\"}");
        int? oldValue = null;
        _device.OnFinalTiltAngleAsync += x => { oldValue = x; return Task.CompletedTask; };

        _device.ParseData("{\"final_tilt_angle\":\"170\"}");

        _device.FinalTiltAngle.Should().Be(170);
        oldValue.Should().Be(30);
    }

    [Fact]
    public void Coordinates_Pass_Old_Value()
    {
        _device.ParseData("{\"coordination\":\"1,2,3\"}");
        (int, int, int)? oldValue = null;
        _device.OnCoordinatesChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        _device.ParseData("{\"coordination\":\"4,-5,6\"}");

        _device.Coordinates.Should().Be((4, -5, 6));
        oldValue.Should().Be((1, 2, 3));
    }

    [Fact]
    public void BedActivity_Passes_Old_Value()
    {
        _device.ParseData("{\"bed_activity\":\"10\"}");
        int? oldValue = null;
        _device.OnBedActivityAsync += x => { oldValue = x; return Task.CompletedTask; };

        _device.ParseData("{\"bed_activity\":\"200\"}");

        _device.BedActivity.Should().Be(200);
        oldValue.Should().Be(10);
    }

    [Fact]
    public void Check_Voltage()
    {
        _device.ParseData("{\"voltage\":3005}");

        _device.Voltage.Should().Be(3.005f);
    }

    [Fact]
    public void SetSensivity_Is_Not_Supported_On_Gateway2()
    {
        _device.Invoking(x => x.SetSensivity(AqaraVibrationSensor.SensivityState.High)).Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("17")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("\"\"")]
    [InlineData("\"1,2\"")]
    [InlineData("\"1,2,3,4\"")]
    [InlineData("\"bad,2,3\"")]
    [InlineData("\"1,bad,3\"")]
    [InlineData("\"1,2,bad\"")]
    [InlineData("\"2147483648,2,3\"")]
    [InlineData("\"1,-2147483649,3\"")]
    [InlineData("\"1,2,2147483648\"")]
    [InlineData("\"1,2,3.5\"")]
    public void Invalid_Coordinates_Preserve_State_And_Do_Not_Prevent_Following_Reports(string rawValue)
    {
        _device.ParseData("{\"coordination\":\"1,2,3\"}");
        var changes = new MiHomeUnitTests.Infrastructure.ValueChangeRecorder<(int, int, int)>(() => _device.Coordinates);
        _device.OnCoordinatesChangeAsync += changes.Handle;

        _device.ParseData($"{{\"coordination\":{rawValue},\"bed_activity\":10}}");

        _device.Coordinates.Should().Be((1, 2, 3));
        changes.Changes.Should().BeEmpty();
        _device.BedActivity.Should().Be(10);
        _loggerFactory.Entries.Should().ContainSingle(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            entry.OriginalFormat == "Coordinates '{Value}' are not 'x,y,z'");

        _device.ParseData("{\"coordination\":\"4,-5,6\"}");
        changes.Changes.Should().Equal(((1, 2, 3), (4, -5, 6)));
        _device.Coordinates.Should().Be((4, -5, 6));
    }

    [Fact]
    public void Coordinates_Accept_Signed_Integer_Boundaries()
    {
        _device.ParseData("{\"coordination\":\"-2147483648,0,2147483647\"}");
        _device.Coordinates.Should().Be((int.MinValue, 0, int.MaxValue));
    }
}
