using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using MiHomeLib.XiaomiGateway3.Devices;
using System.Threading.Tasks;

namespace MiHomeUnitTests.XiaomiGateway3SubDevices;

public class XiaomiHoneywellSmokeDetectorTests: Gw3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"0.1.85\",\"value\":15}]")]
    public void Check_OnSmokeDensityChanged_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<byte>();

        var smokeSensor = new XiaomiHoneywellSmokeDetector(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            SmokeDensity = oldValue
        };
        
        var eventRaised = false;

        smokeSensor.OnSmokeDensityChangedAsync += (x) => 
        { 
            eventRaised = x == oldValue;
            smokeSensor.SmokeDensity.Should().Be((byte)DataToZigbeeResource(data)[0].Value);
            return Task.CompletedTask;
        };

        // Act
        smokeSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":0}]")]
    public void Check_OnSmokeDetected_Event(string data)
    {
        // Arrange
        var eventRaised = false;
        var smokeSensor = new XiaomiHoneywellSmokeDetector(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory);
        
        smokeSensor.OnSmokeDetectedAsync += () => 
        { 
            eventRaised = true;
            smokeSensor.SmokeDetected.Should().Be(DataToZigbeeResource(data)[0].Value == 1);
            return Task.CompletedTask;
        };

        // Act
        smokeSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData("[{\"res_name\":\"14.1.85\",\"value\":67174400}]")]
    [InlineData("[{\"res_name\":\"14.1.85\",\"value\":67239936}]")]
    [InlineData("[{\"res_name\":\"14.1.85\",\"value\":67305472}]")]
    public void Check_OnSmokeSensivityModeChanged_Event(string data)
    {
        // Arrange
        var eventRaised = false;
        var smokeSensor = new XiaomiHoneywellSmokeDetector(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory);
        
        smokeSensor.OnSmokeSensivityModeChanged += (mode) => 
        { 
            eventRaised = true;
            mode.Should().Be((XiaomiHoneywellSmokeDetector.SensivityMode)DataToZigbeeResource(data)[0].Value);
            return Task.CompletedTask;
        };

        // Act
        smokeSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.NoSmoke, 67174400)]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.LowSmoke, 67239936)]
    [InlineData(XiaomiHoneywellSmokeDetector.SensivityMode.MiddleSmoke, 67305472)]
    public void Check_SetSensivity_Works(XiaomiHoneywellSmokeDetector.SensivityMode mode, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}";
        var plug = new XiaomiHoneywellSmokeDetector(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.SetSensivity(mode);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Check_RunSelfTest_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{XiaomiHoneywellSmokeDetector.SELF_TEST_MAGIC_NUMBER}}}]}}";
        var plug = new XiaomiHoneywellSmokeDetector(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.RunSelfTest();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
