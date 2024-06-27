using Xunit;
using AutoFixture;
using FluentAssertions;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;
public class XiaomiMotionSensor2Tests: MiHome3DeviceTests
{
    private readonly XiaomiMotionSensor2 _motionSensor2;

    public XiaomiMotionSensor2Tests() => _motionSensor2 = _fixture.Build<XiaomiMotionSensor2>().Create();

    [Theory]
    [InlineData(15, "000000", XiaomiMotionSensor2.MotionState.Unknown)]
    [InlineData(15, "000100", XiaomiMotionSensor2.MotionState.Unknown)]
    public void Check_OnMotionDetected_Event(int eid, string edata, XiaomiMotionSensor2.MotionState oldState)
    {
        // Arrange       
        var eventRaised = false;

        _motionSensor2.Motion = oldState;
        
        _motionSensor2.OnMotionDetected += (oldValue) =>
         { 
            eventRaised = oldValue == oldState;
            _motionSensor2.Motion.Should().Be((XiaomiMotionSensor2.MotionState)int.Parse(edata));
        };
        
        // Act
        _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4120, "01", XiaomiMotionSensor2.LightState.Unknown)]
    [InlineData(4120, "00", XiaomiMotionSensor2.LightState.Unknown)]
    public void Check_OnLightChange_Event(int eid, string edata, XiaomiMotionSensor2.LightState oldState)
    {
        // Arrange       
        var eventRaised = false;

        _motionSensor2.Light = oldState;
        
        _motionSensor2.OnLightChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldState;
            _motionSensor2.Light.Should().Be((XiaomiMotionSensor2.LightState)int.Parse(edata));
        };
        
        // Act
        _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4119, "78000000", XiaomiMotionSensor2.NoMotionState.Idle120Seconds)]
    [InlineData(4119, "2C010000", XiaomiMotionSensor2.NoMotionState.Idle300Seconds)]
    public void Check_OnNoMotionDetected_Event(int eid, string edata, XiaomiMotionSensor2.NoMotionState state)
    {
        // Arrange       
        var eventRaised = false;

        _motionSensor2.OnNoMotionDetected += (x) =>
        {
            eventRaised = x == state;
        };
        
        // Act
        _motionSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
