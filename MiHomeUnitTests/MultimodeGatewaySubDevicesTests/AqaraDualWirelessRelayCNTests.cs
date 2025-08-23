using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class AqaraDualWirelessRelayCNTests: MultimodeGatewayDeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"4.1.85\",\"value\":1}]")]
    public void Check_OnChannel1StateChange_Event(string data)
    {
        // Arrange  
        var relay = new AqaraDualWirelessRelayCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Channel1 = AqaraDualWirelessRelayCN.ChannelState.Off
        };

        var eventRaised = false;
        var counter = 0;

        relay.OnChannel1StateChangeAsync += () => 
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

    [Theory, InlineData("[{\"res_name\":\"4.2.85\",\"value\":1}]")]
    public void Check_OnChannel2StateChange_Event(string data)
    {
        // Arrange  
        var relay = new AqaraDualWirelessRelayCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Channel2 = AqaraDualWirelessRelayCN.ChannelState.Off
        };

        var eventRaised = false;
        var counter = 0;

        relay.OnChannel2StateChangeAsync += () => 
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
    
    [Theory, InlineData("[{\"res_name\":\"0.11.85\",\"value\":212512}]")]
    public void Check_OnVoltageChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraDualWirelessRelayCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Voltage = oldValue
        };
        
        var eventRaised = false;

        relay.OnVoltageChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.Voltage.Should().Be(GetMiSpecValue<int>(data)/1000f);
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
    
    [Theory, InlineData("[{\"res_name\":\"0.12.85\",\"value\":42.2}]")]
    public void Check_OnLoadPowerChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraDualWirelessRelayCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
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

    [Theory, InlineData("[{\"res_name\":\"0.13.85\",\"value\":3163.13}]")]
    public void Check_OnEnergyChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraDualWirelessRelayCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Energy = oldValue
        };
        
        var eventRaised = false;

        relay.OnEnergyChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.Energy.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.14.85\",\"value\":153.24}]")]
    public void Check_OnCurrentChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraDualWirelessRelayCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Current = oldValue
        };
        
        var eventRaised = false;

        relay.OnCurrentChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.Current.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
    
    [Fact]
    public void Check_Channel1PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}";
        var relay = new AqaraDualWirelessRelayCN(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        relay.Channel1PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Check_Channel2PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.2.85\",\"value\":1}}]}}";
        var relay = new AqaraDualWirelessRelayCN(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        relay.Channel2PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.Off, 1)]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.On, 0)]
    public void Check_Channel1ToggleState_Works(AqaraDualWirelessRelayCN.ChannelState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":{value}}}]}}";
        var relay = new AqaraDualWirelessRelayCN(did, _mqttTransport.Object, _loggerFactory)
        {
            Channel1 = state
        };

        // Act
        relay.Channel1ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.Off, 1)]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.On, 0)]
    public void Check_Channel2ToggleState_Works(AqaraDualWirelessRelayCN.ChannelState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.2.85\",\"value\":{value}}}]}}";
        var relay = new AqaraDualWirelessRelayCN(did, _mqttTransport.Object, _loggerFactory)
        {
            Channel2 = state
        };

        // Act
        relay.Channel2ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.PowerMemoryState.PowerOff, 0)]
    [InlineData(AqaraDualWirelessRelayCN.PowerMemoryState.Previous, 1)]
    public void Check_SetPowerMemoryState_Works(AqaraDualWirelessRelayCN.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2030\",\"value\":{value}}}]}}";
        var relay = new AqaraDualWirelessRelayCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        relay.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.InterlockState.Disabled, 0)]
    [InlineData(AqaraDualWirelessRelayCN.InterlockState.Enabled, 1)]
    public void Check_SetInterlock_Works(AqaraDualWirelessRelayCN.InterlockState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.9.85\",\"value\":{value}}}]}}";
        var relay = new AqaraDualWirelessRelayCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        relay.SetInterlock(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
