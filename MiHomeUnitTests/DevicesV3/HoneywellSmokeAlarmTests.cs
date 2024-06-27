using Xunit;
using AutoFixture;
using FluentAssertions;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;

public class HoneywellSmokeAlarmTests: MiHome3DeviceTests
{
    private readonly HoneywellSmokeAlarm _smokeAlarm;

    public HoneywellSmokeAlarmTests() => _smokeAlarm = _fixture.Build<HoneywellSmokeAlarm>().Create();

    [Theory]
    [InlineData(4117, "00", HoneywellSmokeAlarm.SmokeState.Unknown)]
    [InlineData(4117, "01", HoneywellSmokeAlarm.SmokeState.NoSmokeDetected)]
    public void Check_OnSmokeChange_Event(int eid, string edata, HoneywellSmokeAlarm.SmokeState oldState)
    {
        // Arrange       
        var eventRaised = false;

        _smokeAlarm.Smoke = oldState;
        
        _smokeAlarm.OnSmokeChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldState;
            _smokeAlarm.Smoke.Should().Be((HoneywellSmokeAlarm.SmokeState)int.Parse(edata));
        };
        
        // Act
        _smokeAlarm.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
