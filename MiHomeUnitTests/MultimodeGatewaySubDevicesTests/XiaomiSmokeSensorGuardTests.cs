using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class XiaomiSmokeSensorGuardTests: MultimodeGatewayDeviceTests
{
    private readonly XiaomiSmokeSensorGuard _smokeAlarm;

    public XiaomiSmokeSensorGuardTests() => _smokeAlarm = _fixture.Build<XiaomiSmokeSensorGuard>().Create();

    [Theory]
    [InlineData(4117, "00", XiaomiSmokeSensorGuard.SmokeState.Unknown)]
    [InlineData(4117, "01", XiaomiSmokeSensorGuard.SmokeState.NoSmokeDetected)]
    public void Check_OnSmokeChange_Event(int eid, string edata, XiaomiSmokeSensorGuard.SmokeState oldState)
    {
        // Arrange       
        var eventRaised = false;

        _smokeAlarm.Smoke = oldState;
        
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
}
