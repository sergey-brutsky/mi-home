using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;

public class AqaraWaterLeakSensorTests: MiHome3DeviceTests
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

        wl.OnMoistureChange += () => 
        { 
            eventRaised = true;
            wl.Moisture.Should().Be(DataToZigbeeResource(data)[0].Value == 1);
        };

        // Act
        wl.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
