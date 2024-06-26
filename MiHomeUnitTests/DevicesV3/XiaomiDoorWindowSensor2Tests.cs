using Xunit;
using AutoFixture;
using FluentAssertions;
using System;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;

public class XiaomiDoorWindowSensor2Tests: MiHome3DeviceTests
{
    private readonly XiaomiDoorWindowSensor2 _dwSensor2;

    public XiaomiDoorWindowSensor2Tests()
    {
        _dwSensor2 = _fixture.Build<XiaomiDoorWindowSensor2>().Create();
    }

    [Theory]
    [InlineData(4121, "00", XiaomiDoorWindowSensor2.ContactState.Unknown)]
    [InlineData(4121, "01", XiaomiDoorWindowSensor2.ContactState.Open)]
    [InlineData(4121, "02", XiaomiDoorWindowSensor2.ContactState.Unknown)]
    public void Check_OnContactChange_Event(int eid, string edata, XiaomiDoorWindowSensor2.ContactState oldState)
    {
        // Arrange       
        var eventRaised = false;

        _dwSensor2.Contact = oldState;
        
        _dwSensor2.OnContactChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldState;
            _dwSensor2.Contact.Should().Be((XiaomiDoorWindowSensor2.ContactState)int.Parse(edata));
        };
        
        // Act
        _dwSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4120, "00", XiaomiDoorWindowSensor2.LightState.LightDiscovered)]
    [InlineData(4120, "01", XiaomiDoorWindowSensor2.LightState.NoLight)]
    public void Check_OnLightChange_Event(int eid, string edata, XiaomiDoorWindowSensor2.LightState oldState)
    {
        // Arrange       
        var eventRaised = false;
        
        _dwSensor2.Light = oldState;
        
        _dwSensor2.OnLightChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldState;
            _dwSensor2.Light.Should().Be((XiaomiDoorWindowSensor2.LightState)int.Parse(edata));
        };
        
        // Act
        _dwSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }    
}
