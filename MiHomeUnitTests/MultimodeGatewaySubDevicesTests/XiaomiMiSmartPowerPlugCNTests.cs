using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
public class XiaomiMiSmartPowerPlugCNTests: MultimodeGatewayDeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"4.1.85\",\"value\":1}]")]
    public void Check_OnStateChange_Event(string data)
    {
        // Arrange 
        var plug = new XiaomiMiSmartPowerPlugCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            State = XiaomiMiSmartPowerPlugCN.PlugState.Off
        };
        
        var eventRaised = false;
        var counter = 0;

        plug.OnStateChangeAsync += () => 
        { 
            counter++;
            eventRaised = true;
            return Task.CompletedTask;
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
        var plug = new XiaomiMiSmartPowerPlugCN(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            LoadPower = oldValue
        };
        
        var eventRaised = false;

        plug.OnLoadPowerChangeAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            plug.LoadPower.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
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
        var plug = new XiaomiMiSmartPowerPlugCN(did, _mqttTransport.Object, _loggerFactory);
        
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
        var plug = new XiaomiMiSmartPowerPlugCN(did, _mqttTransport.Object, _loggerFactory);
        
        // Act
        plug.PowerOff();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiMiSmartPowerPlugCN.PlugState.Off, 1)]
    [InlineData(XiaomiMiSmartPowerPlugCN.PlugState.On, 0)]
    public void Check_Toggle_Works(XiaomiMiSmartPowerPlugCN.PlugState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":{value}}}]}}";
        var plug = new XiaomiMiSmartPowerPlugCN(did, _mqttTransport.Object, _loggerFactory)
        {
            State = state
        };

        // Act
        plug.ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiMiSmartPowerPlugCN.PowerMemoryState.PowerOff, 0)]
    [InlineData(XiaomiMiSmartPowerPlugCN.PowerMemoryState.Previous, 1)]
    public void Check_PowerOnState_Works(XiaomiMiSmartPowerPlugCN.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2030\",\"value\":{value}}}]}}";
        var plug = new XiaomiMiSmartPowerPlugCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiMiSmartPowerPlugCN.ChargeProtect.Off, 0)]
    [InlineData(XiaomiMiSmartPowerPlugCN.ChargeProtect.On, 1)]
    public void Check_ChargeProtection_Works(XiaomiMiSmartPowerPlugCN.ChargeProtect state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2031\",\"value\":{value}}}]}}";
        var plug = new XiaomiMiSmartPowerPlugCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetChargeProtection(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiMiSmartPowerPlugCN.LedState.TurnOffAtNightTime, 0)]
    [InlineData(XiaomiMiSmartPowerPlugCN.LedState.AlwaysOn, 1)]
    public void Check_SetLedState_Works(XiaomiMiSmartPowerPlugCN.LedState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2032\",\"value\":{value}}}]}}";
        var plug = new XiaomiMiSmartPowerPlugCN(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetLedState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
