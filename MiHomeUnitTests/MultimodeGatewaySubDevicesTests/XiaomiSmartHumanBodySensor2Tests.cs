using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
public class XiaomiSmartHumanBodySensor2Tests: MultimodeGatewayDeviceTests
{
    private readonly XiaomiSmartHumanBodySensor2 _motionSensor2;

    public XiaomiSmartHumanBodySensor2Tests() => _motionSensor2 = _fixture.Build<XiaomiSmartHumanBodySensor2>().Create();

    [Theory]
    [InlineData(15, "000000", XiaomiSmartHumanBodySensor2.MotionState.Unknown)]
    [InlineData(15, "000100", XiaomiSmartHumanBodySensor2.MotionState.Unknown)]
    public void Check_OnMotionDetected_Event(int eid, string edata, XiaomiSmartHumanBodySensor2.MotionState oldState)
    {
        // Arrange       
        var eventRaised = false;

        _motionSensor2.Motion = oldState;
        
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

        _motionSensor2.Light = oldState;
        
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
}
