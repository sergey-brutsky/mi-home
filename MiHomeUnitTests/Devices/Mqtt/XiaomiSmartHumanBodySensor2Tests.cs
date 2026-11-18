using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;

namespace MiHomeUnitTests.Devices.Mqtt;
public class XiaomiSmartHumanBodySensor2Tests: ProtocolDeviceTestBase
{
    private readonly XiaomiSmartHumanBodySensor2 _motionSensor2;

    public XiaomiSmartHumanBodySensor2Tests() => _motionSensor2 = CreateLumiBleProtocolDevice<XiaomiSmartHumanBodySensor2>(XiaomiSmartHumanBodySensor2.PDID);

    [Theory]
    [InlineData(15, "000000", XiaomiSmartHumanBodySensor2.MotionState.Unknown)]
    [InlineData(15, "000100", XiaomiSmartHumanBodySensor2.MotionState.Unknown)]
    public void Check_OnMotionDetected_Event(int eid, string edata, XiaomiSmartHumanBodySensor2.MotionState oldState)
    {
        // Arrange
        var eventRaised = false;

        _motionSensor2.Motion.Should().Be(oldState);

        _motionSensor2.OnMotionDetectedAsync += (oldValue) =>
         {
            eventRaised = oldValue == oldState;
            _motionSensor2.Motion.Should().Be((XiaomiSmartHumanBodySensor2.MotionState)int.Parse(edata));
            return Task.CompletedTask;
        };

        // Act
        _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4120, "01", XiaomiSmartHumanBodySensor2.LightState.Unknown)]
    [InlineData(4120, "00", XiaomiSmartHumanBodySensor2.LightState.Unknown)]
    public void Check_OnLightChange_Event(int eid, string edata, XiaomiSmartHumanBodySensor2.LightState oldState)
    {
        // Arrange
        var eventRaised = false;

        _motionSensor2.Light.Should().Be(oldState);

        _motionSensor2.OnLightChangeAsync += (oldValue) =>
         {
            eventRaised = oldValue == oldState;
            _motionSensor2.Light.Should().Be((XiaomiSmartHumanBodySensor2.LightState)int.Parse(edata));
            return Task.CompletedTask;
        };

        // Act
        _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4119, "78000000", XiaomiSmartHumanBodySensor2.NoMotionState.Idle120Seconds)]
    [InlineData(4119, "2C010000", XiaomiSmartHumanBodySensor2.NoMotionState.Idle300Seconds)]
    public void Check_OnNoMotionDetected_Event(int eid, string edata, XiaomiSmartHumanBodySensor2.NoMotionState state)
    {
        // Arrange
        var eventRaised = false;

        _motionSensor2.OnNoMotionDetectedAsync += (x) =>
        {
            eventRaised = x == state;
            return Task.CompletedTask;
        };

        // Act
        _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
    [Fact]
    public void Motion_And_Light_Repeats_Are_Suppressed()
    {
        var motions = new System.Collections.Generic.List<XiaomiSmartHumanBodySensor2.MotionState>();
        var lights = new System.Collections.Generic.List<XiaomiSmartHumanBodySensor2.LightState>();
        _motionSensor2.OnMotionDetectedAsync += old => { motions.Add(old); return Task.CompletedTask; };
        _motionSensor2.OnLightChangeAsync += old => { lights.Add(old); return Task.CompletedTask; };
        foreach (var data in new[] { "000000", "000000", "000100", "000100" })
            _motionSensor2.ParseData(SetupBleAsyncEventParams(15, data).ToString());
        foreach (var data in new[] { "00", "00", "01", "01" })
            _motionSensor2.ParseData(SetupBleAsyncEventParams(4120, data).ToString());
        motions.Should().Equal(XiaomiSmartHumanBodySensor2.MotionState.Unknown, XiaomiSmartHumanBodySensor2.MotionState.MotionWithoutLight);
        lights.Should().Equal(XiaomiSmartHumanBodySensor2.LightState.Unknown, XiaomiSmartHumanBodySensor2.LightState.LightOff);
        _motionSensor2.Motion.Should().Be(XiaomiSmartHumanBodySensor2.MotionState.MotionWithLight);
        _motionSensor2.Light.Should().Be(XiaomiSmartHumanBodySensor2.LightState.LightOn);
    }

    [Fact]
    public void Idle_Reports_Are_Not_Deduplicated()
    {
        var count = 0;
        _motionSensor2.OnNoMotionDetectedAsync += duration => { duration.Should().Be(XiaomiSmartHumanBodySensor2.NoMotionState.Idle300Seconds); count++; return Task.CompletedTask; };
        var report = SetupBleAsyncEventParams(4119, "2c010000").ToString();
        _motionSensor2.ParseData(report);
        _motionSensor2.ParseData(report);
        count.Should().Be(2);
    }

    [Fact]
    public void Battery_Reports_Update_Description_And_Pass_Previous_Value()
    {
        var previous = new System.Collections.Generic.List<byte>();
        _motionSensor2.ToString().Should().Contain("BatteryPercent: Unknown");
        _motionSensor2.OnBatteryPercentChangeAsync += old => { previous.Add(old); return Task.CompletedTask; };
        foreach (var data in new[] { "00", "64", "64" })
            _motionSensor2.ParseData(SetupBleAsyncEventParams(4106, data).ToString());
        previous.Should().Equal(0, 0, 100);
        _motionSensor2.ToString().Should().Contain("BatteryPercent: 100%");
    }

    [Theory]
    [InlineData(15, "0000")]
    [InlineData(4120, "0001")]
    [InlineData(4119, "7800")]
    [InlineData(4106, "0000")]
    public void Invalid_Length_Does_Not_Update_State(int eid, string data)
    {
        var before = _motionSensor2.ToString();
        System.Action parse = () => _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, data).ToString());
        parse.Should().NotThrow();
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains(eid.ToString()));
        _motionSensor2.ToString().Should().Be(before);
    }
    [Fact]
    public void Unknown_Codes_Produce_Unknown_States()
    {
        _motionSensor2.ParseData(SetupBleAsyncEventParams(15, "000100").ToString());
        _motionSensor2.ParseData(SetupBleAsyncEventParams(4120, "01").ToString());
        _motionSensor2.ParseData(SetupBleAsyncEventParams(15, "ffffff").ToString());
        _motionSensor2.ParseData(SetupBleAsyncEventParams(4120, "FF").ToString());
        _motionSensor2.Motion.Should().Be(XiaomiSmartHumanBodySensor2.MotionState.Unknown);
        _motionSensor2.Light.Should().Be(XiaomiSmartHumanBodySensor2.LightState.Unknown);
    }
}
