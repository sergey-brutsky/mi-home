using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using Moq;

namespace MiHomeUnitTests.DevicesV3;

public class AqaraTwoChannelsRelayTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"4.1.85\",\"value\":1}]")]
    public void Check_OnChannel1StateChange_Event(string data)
    {
        // Arrange  
        var relay = new AqaraTwoChannelsRelay(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Channel1 = AqaraTwoChannelsRelay.ChannelState.Off
        };

        var eventRaised = false;
        var counter = 0;

        relay.OnChannel1StateChange += () => 
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

    [Theory, InlineData("[{\"res_name\":\"4.2.85\",\"value\":1}]")]
    public void Check_OnChannel2StateChange_Event(string data)
    {
        // Arrange  
        var relay = new AqaraTwoChannelsRelay(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Channel2 = AqaraTwoChannelsRelay.ChannelState.Off
        };

        var eventRaised = false;
        var counter = 0;

        relay.OnChannel2StateChange += () => 
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
    
    [Theory, InlineData("[{\"res_name\":\"0.11.85\",\"value\":212512}]")]
    public void Check_OnVoltageChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraTwoChannelsRelay(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Voltage = oldValue
        };
        
        var eventRaised = false;

        relay.OnVoltageChange += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.Voltage.Should().Be(GetMiSpecValue<int>(data)/1000f);
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
        var relay = new AqaraTwoChannelsRelay(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
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

    [Theory, InlineData("[{\"res_name\":\"0.13.85\",\"value\":3163.13}]")]
    public void Check_OnEnergyChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var relay = new AqaraTwoChannelsRelay(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Energy = oldValue
        };
        
        var eventRaised = false;

        relay.OnEnergyChange += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.Energy.Should().Be(GetMiSpecValue<float>(data));
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
        var relay = new AqaraTwoChannelsRelay(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            Current = oldValue
        };
        
        var eventRaised = false;

        relay.OnCurrentChange += (x) => 
        { 
            eventRaised = x == oldValue;
            relay.Current.Should().Be(GetMiSpecValue<float>(data));
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
        var relay = new AqaraTwoChannelsRelay(did, _mqttTransport.Object, _loggerFactory);
        
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
        var relay = new AqaraTwoChannelsRelay(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        relay.Channel2PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraTwoChannelsRelay.ChannelState.Off, 1)]
    [InlineData(AqaraTwoChannelsRelay.ChannelState.On, 0)]
    public void Check_Channel1ToggleState_Works(AqaraTwoChannelsRelay.ChannelState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":{value}}}]}}";
        var relay = new AqaraTwoChannelsRelay(did, _mqttTransport.Object, _loggerFactory)
        {
            Channel1 = state
        };

        // Act
        relay.Channel1ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraTwoChannelsRelay.ChannelState.Off, 1)]
    [InlineData(AqaraTwoChannelsRelay.ChannelState.On, 0)]
    public void Check_Channel2ToggleState_Works(AqaraTwoChannelsRelay.ChannelState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.2.85\",\"value\":{value}}}]}}";
        var relay = new AqaraTwoChannelsRelay(did, _mqttTransport.Object, _loggerFactory)
        {
            Channel2 = state
        };

        // Act
        relay.Channel2ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraTwoChannelsRelay.PowerMemoryState.PowerOff, 0)]
    [InlineData(AqaraTwoChannelsRelay.PowerMemoryState.Previous, 1)]
    public void Check_SetPowerMemoryState_Works(AqaraTwoChannelsRelay.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2030\",\"value\":{value}}}]}}";
        var relay = new AqaraTwoChannelsRelay(did, _mqttTransport.Object, _loggerFactory);

        // Act
        relay.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraTwoChannelsRelay.InterlockState.Disabled, 0)]
    [InlineData(AqaraTwoChannelsRelay.InterlockState.Enabled, 1)]
    public void Check_SetInterlock_Works(AqaraTwoChannelsRelay.InterlockState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.9.85\",\"value\":{value}}}]}}";
        var relay = new AqaraTwoChannelsRelay(did, _mqttTransport.Object, _loggerFactory);

        // Act
        relay.SetInterlock(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
