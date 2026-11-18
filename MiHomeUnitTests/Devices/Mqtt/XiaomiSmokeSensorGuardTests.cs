using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiSmokeSensorGuardTests: ProtocolDeviceTestBase
{
    private readonly XiaomiSmokeSensorGuard _smokeAlarm;

    public XiaomiSmokeSensorGuardTests() => _smokeAlarm = CreateLumiBleProtocolDevice<XiaomiSmokeSensorGuard>(XiaomiSmokeSensorGuard.PDID);

    [Theory]
    [InlineData(4117, "00", XiaomiSmokeSensorGuard.SmokeState.Unknown)]
    [InlineData(4117, "01", XiaomiSmokeSensorGuard.SmokeState.NoSmokeDetected)]
    public void Check_OnSmokeChange_Event(int eid, string edata, XiaomiSmokeSensorGuard.SmokeState oldState)
    {
        // Arrange
        var eventRaised = false;

        if (oldState != XiaomiSmokeSensorGuard.SmokeState.Unknown)
            _smokeAlarm.ParseData(SetupBleAsyncEventParams(4117, ((int)oldState).ToString("x2")).ToString());

        _smokeAlarm.OnSmokeChangeAsync += (oldValue) =>
         {
            eventRaised = oldValue == oldState;
            _smokeAlarm.Smoke.Should().Be((XiaomiSmokeSensorGuard.SmokeState)int.Parse(edata));
            return Task.CompletedTask;
        };

        // Act
        _smokeAlarm.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
    [Fact]
    public void Smoke_Transitions_Pass_Previous_State_And_Suppress_Repeats()
    {
        var previous = new System.Collections.Generic.List<XiaomiSmokeSensorGuard.SmokeState>();
        _smokeAlarm.OnSmokeChangeAsync += old => { previous.Add(old); return Task.CompletedTask; };
        foreach (var data in new[] { "00", "00", "01", "01", "00" })
            _smokeAlarm.ParseData(SetupBleAsyncEventParams(4117, data).ToString());
        previous.Should().Equal(XiaomiSmokeSensorGuard.SmokeState.Unknown,
            XiaomiSmokeSensorGuard.SmokeState.NoSmokeDetected, XiaomiSmokeSensorGuard.SmokeState.SmokeDetected);
        _smokeAlarm.Smoke.Should().Be(XiaomiSmokeSensorGuard.SmokeState.NoSmokeDetected);
    }

    [Fact]
    public void Battery_Reports_Are_Independent_Of_Smoke_And_Keep_Repeats()
    {
        var previous = new System.Collections.Generic.List<byte>();
        _smokeAlarm.ToString().Should().Contain("BatteryPercent: Unknown");
        _smokeAlarm.ParseData(SetupBleAsyncEventParams(4117, "01").ToString());
        _smokeAlarm.ToString().Should().Contain("BatteryPercent: Unknown");
        _smokeAlarm.OnBatteryPercentChangeAsync += old => { previous.Add(old); return Task.CompletedTask; };
        foreach (var data in new[] { "00", "64", "64" })
            _smokeAlarm.ParseData(SetupBleAsyncEventParams(4106, data).ToString());
        previous.Should().Equal(0, 0, 100);
        _smokeAlarm.BatteryPercent.Should().Be(100);
        _smokeAlarm.ToString().Should().Contain("BatteryPercent: 100%");
        _smokeAlarm.Smoke.Should().Be(XiaomiSmokeSensorGuard.SmokeState.SmokeDetected);
    }

    [Theory]
    [InlineData(4117)]
    [InlineData(4106)]
    public void Invalid_Length_Does_Not_Update_State(int eid)
    {
        var before = _smokeAlarm.ToString();
        System.Action parse = () => _smokeAlarm.ParseData(SetupBleAsyncEventParams(eid, "0001").ToString());
        parse.Should().NotThrow();
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains(eid.ToString()));
        _smokeAlarm.ToString().Should().Be(before);
    }

    [Fact]
    public void Unknown_Hex_State_Produces_Unknown()
    {
        _smokeAlarm.ParseData(SetupBleAsyncEventParams(4117, "01").ToString());
        _smokeAlarm.ParseData(SetupBleAsyncEventParams(4117, "FF").ToString());
        _smokeAlarm.Smoke.Should().Be(XiaomiSmokeSensorGuard.SmokeState.Unknown);
    }
}
