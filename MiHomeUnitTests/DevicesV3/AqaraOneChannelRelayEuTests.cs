using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using Moq;
using System;

namespace MiHomeUnitTests.DevicesV3;
public class AqaraOneChannelRelayEuTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"siid\":2,\"piid\":1,\"value\":true}]")]
    public void Check_OnChannelStateChange_Event(string data)
    {
        // Arrange  
        var relay = new AqaraOneChannelRelayEu(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            State = AqaraOneChannelRelayEu.RelayState.Off
        };

        var eventRaised = false;
        var counter = 0;

        relay.OnStateChange += () => 
        { 
            counter++;
            eventRaised = true;
        };

        // Act
        relay.ParseData(data);
        relay.ParseData(data); // this is intentionally

        // Assert
        eventRaised.Should().BeTrue();
        counter.Should().Be(1);
    }

    [Theory, InlineData("[{\"siid\":3,\"piid\":2,\"value\":42.20}]")]
    public void Check_OnLoadPowerChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        
        var relay = new AqaraOneChannelRelayEu(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            LoadPower = oldValue
        };

        var eventRaised = false;

        relay.OnLoadPowerChange += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.LoadPower.Should().Be(GetMiSpecValue<float>(data));
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"siid\":3,\"piid\":1,\"value\":1.30}]")]
    public void Check_OnPowerConsumptionChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraOneChannelRelayEu(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            PowerConsumption = oldValue
        };

        var eventRaised = false;

        relay.OnPowerConsumptionChange += (x) =>
        {
            eventRaised = x == oldValue;
            relay.PowerConsumption.Should().Be(GetMiSpecValue<float>(data));
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }    

    [Fact]
    public void Check_PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":1}}]}}";
        var relay = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        relay.PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Check_PowerOff_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":0}}]}}";
        var relay = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        relay.PowerOff();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
    [Theory]
    [InlineData(AqaraOneChannelRelayEu.RelayState.Off, 1)]
    [InlineData(AqaraOneChannelRelayEu.RelayState.On, 0)]
    public void Check_Toggle_Works(AqaraOneChannelRelayEu.RelayState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":{value}}}]}}";
        var plug = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory)
        {
            State = state
        };

        // Act
        plug.ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraOneChannelRelayEu.PowerMemoryState.PowerOff, 0)]
    [InlineData(AqaraOneChannelRelayEu.PowerMemoryState.Previous, 1)]
    public void Check_SetPowerMemoryState_Works(AqaraOneChannelRelayEu.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":5,\"piid\":1,\"value\":{value}}}]}}";
        var plug = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraOneChannelRelayEu.PowerMode.Momentary, 1)]
    [InlineData(AqaraOneChannelRelayEu.PowerMode.Toggle, 2)]
    public void Check_SetPowerMode_Works(AqaraOneChannelRelayEu.PowerMode mode, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":7,\"piid\":2,\"value\":{value}}}]}}";
        var plug = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetPowerMode(mode);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
    
    [Fact]
    public void SetPowerOverloadThreshold_When_ValidArgument_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var value = 100;
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":5,\"piid\":6,\"value\":{value}}}]}}";
        var plug = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetPowerOverloadThreshold(value);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void SetPowerOverloadThreshold_When_InvalidArgument_ThrowsException()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var value = 5000; // invalid argument
        var plug = new AqaraOneChannelRelayEu(did, _mqttTransport.Object, _loggerFactory);

        // Act
        Action code = () => { plug.SetPowerOverloadThreshold(value); };

        // Assert
        code
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>()
            .WithParameterName("threshold")
            .Which.ActualValue.Should().Be(value);
    }    
}
