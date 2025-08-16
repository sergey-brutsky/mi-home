using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using System;
using MiHomeLib.XiaomiGateway3.Devices;
using System.Threading.Tasks;

namespace MiHomeUnitTests.XiaomiGateway3SubDevices;
public class AqaraT1WithNeutralTests: Gw3DeviceTests
{
    [Theory, InlineData("[{\"siid\":2,\"piid\":1,\"value\":true}]")]
    public void Check_OnChannelStateChange_Event(string data)
    {
        // Arrange  
        var relay = new AqaraT1WithNeutral(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            State = AqaraT1WithNeutral.RelayState.Off
        };

        var eventRaised = false;
        var counter = 0;

        relay.OnStateChangeAsync += () => 
        { 
            counter++;
            eventRaised = true;
            return Task.CompletedTask;
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
        
        var relay = new AqaraT1WithNeutral(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            LoadPower = oldValue
        };

        var eventRaised = false;

        relay.OnLoadPowerChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.LoadPower.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
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
        var relay = new AqaraT1WithNeutral(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            PowerConsumption = oldValue
        };

        var eventRaised = false;

        relay.OnPowerConsumptionChangeAsync += (x) =>
        {
            eventRaised = x == oldValue;
            relay.PowerConsumption.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
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
        var relay = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory);
        
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
        var relay = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        relay.PowerOff();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
    [Theory]
    [InlineData(AqaraT1WithNeutral.RelayState.Off, 1)]
    [InlineData(AqaraT1WithNeutral.RelayState.On, 0)]
    public void Check_Toggle_Works(AqaraT1WithNeutral.RelayState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":{value}}}]}}";
        var plug = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory)
        {
            State = state
        };

        // Act
        plug.ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraT1WithNeutral.PowerMemoryState.PowerOff, 0)]
    [InlineData(AqaraT1WithNeutral.PowerMemoryState.Previous, 1)]
    public void Check_SetPowerMemoryState_Works(AqaraT1WithNeutral.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":5,\"piid\":1,\"value\":{value}}}]}}";
        var plug = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraT1WithNeutral.PowerMode.Momentary, 1)]
    [InlineData(AqaraT1WithNeutral.PowerMode.Toggle, 2)]
    public void Check_SetPowerMode_Works(AqaraT1WithNeutral.PowerMode mode, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":7,\"piid\":2,\"value\":{value}}}]}}";
        var plug = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory);

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
        var plug = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory);

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
        var plug = new AqaraT1WithNeutral(did, _mqttTransport.Object, _loggerFactory);

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
