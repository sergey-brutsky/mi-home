using AutoFixture;
using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class AqaraWaterLeakSensorTests: MultimodeGatewayDeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnMoistureChange_Event(string data)
    {
        // Arrange  
        var wl = _fixture
                    .Build<AqaraWaterLeakSensor>()
                    .Create();
        
        var eventRaised = false;

        wl.OnMoistureChangeAsync += () => 
        { 
            eventRaised = true;
            wl.Moisture.Should().Be(DataToZigbeeResource(data)[0].Value == 1);
            return Task.CompletedTask;
        };

        // Act
        wl.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
