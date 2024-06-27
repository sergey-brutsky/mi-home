using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using Moq;

namespace MiHomeUnitTests.DevicesV3;

public class HoneywellSmokeSensorTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"0.1.85\",\"value\":15}]")]
    public void Check_OnSmokeDensityChanged_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<byte>();

        var smokeSensor = new HoneywellSmokeSensor(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory)
        {
            SmokeDensity = oldValue
        };
        
        var eventRaised = false;

        smokeSensor.OnSmokeDensityChanged += (x) => 
        { 
            eventRaised = x == oldValue;
            smokeSensor.SmokeDensity.Should().Be((byte)DataToZigbeeResource(data)[0].Value);
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
        var smokeSensor = new HoneywellSmokeSensor(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory);
        
        smokeSensor.OnSmokeDetected += () => 
        { 
            eventRaised = true;
            smokeSensor.SmokeDetected.Should().Be(DataToZigbeeResource(data)[0].Value == 1);
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
        var smokeSensor = new HoneywellSmokeSensor(_fixture.Create<string>(), _mqttTransport.Object, _loggerFactory);
        
        smokeSensor.OnSmokeSensivityModeChanged += (mode) => 
        { 
            eventRaised = true;
            mode.Should().Be((HoneywellSmokeSensor.SensivityMode)DataToZigbeeResource(data)[0].Value);
        };

        // Act
        smokeSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(HoneywellSmokeSensor.SensivityMode.NoSmoke, 67174400)]
    [InlineData(HoneywellSmokeSensor.SensivityMode.LowSmoke, 67239936)]
    [InlineData(HoneywellSmokeSensor.SensivityMode.MiddleSmoke, 67305472)]
    public void Check_SetSensivity_Works(HoneywellSmokeSensor.SensivityMode mode, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{value}}}]}}";
        var plug = new HoneywellSmokeSensor(did, _mqttTransport.Object, _loggerFactory);

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
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"14.1.85\",\"value\":{HoneywellSmokeSensor.SELF_TEST_MAGIC_NUMBER}}}]}}";
        var plug = new HoneywellSmokeSensor(did, _mqttTransport.Object, _loggerFactory);

        // Act
        plug.RunSelfTest();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
