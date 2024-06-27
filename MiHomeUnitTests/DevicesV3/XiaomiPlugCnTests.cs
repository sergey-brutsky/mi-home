using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using Moq;

namespace MiHomeUnitTests.DevicesV3;
public class XiaomiPlugCnTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"4.1.85\",\"value\":1}]")]
    public void Check_OnStateChange_Event(string data)
    {
        // Arrange 
        var plug = new XiaomiPlugCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            State = XiaomiPlugCN.PlugState.Off
        };
        
        var eventRaised = false;
        var counter = 0;

        plug.OnStateChange += () => 
        { 
            counter++;
            eventRaised = true;
        };

        // Act
        plug.ParseData(data);
        plug.ParseData(data); // this is intentionally

        // Assert
        eventRaised.Should().BeTrue();
        counter.Should().Be(1);
    }

    [Theory, InlineData("[{\"res_name\":\"0.12.85\",\"value\":42.2}]")]
    public void Check_OnLoadPowerChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var plug = new XiaomiPlugCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            LoadPower = oldValue
        };
        
        var eventRaised = false;

        plug.OnLoadPowerChange += (x) => 
        { 
            eventRaised = x == oldValue;
            plug.LoadPower.Should().Be(GetMiSpecValue<float>(data));
        };

        // Act
        plug.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
    [Fact]
    public void Check_PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}";
        var plug = new XiaomiPlugCN(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        plug.PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
    [Fact]
    public void Check_PowerOff_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":0}}]}}";
        var plug = new XiaomiPlugCN(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        plug.PowerOff();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiPlugCN.PlugState.Off, 1)]
    [InlineData(XiaomiPlugCN.PlugState.On, 0)]
    public void Check_Toggle_Works(XiaomiPlugCN.PlugState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":{value}}}]}}";
        var plug = new XiaomiPlugCN(did, _mqttTransport.Object, _loggerFactory)
        {
            State = state
        };

        // Act
        plug.ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiPlugCN.PowerMemoryState.PowerOff, 0)]
    [InlineData(XiaomiPlugCN.PowerMemoryState.Previous, 1)]
    public void Check_PowerOnState_Works(XiaomiPlugCN.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2030\",\"value\":{value}}}]}}";
        var plug = new XiaomiPlugCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiPlugCN.ChargeProtect.Off, 0)]
    [InlineData(XiaomiPlugCN.ChargeProtect.On, 1)]
    public void Check_ChargeProtection_Works(XiaomiPlugCN.ChargeProtect state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2031\",\"value\":{value}}}]}}";
        var plug = new XiaomiPlugCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetChargeProtection(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiPlugCN.LedState.TurnOffAtNightTime, 0)]
    [InlineData(XiaomiPlugCN.LedState.AlwaysOn, 1)]
    public void Check_SetLedState_Works(XiaomiPlugCN.LedState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2032\",\"value\":{value}}}]}}";
        var plug = new XiaomiPlugCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetLedState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
