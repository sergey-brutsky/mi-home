using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;
using static MiHomeLib.DevicesV3.XiaomiDoorWindowSensor;

namespace MiHomeUnitTests.DevicesV3;

public class XiaomiDoorWindowSensorTests: MiHome3DeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnContactChange_Event(string data)
    {
        // Arrange  
        var dwSensor = _fixture
                    .Build<XiaomiDoorWindowSensor>()
                    .Create();
        
        var eventRaised = false;

        dwSensor.OnContactChanged += () => 
        { 
            eventRaised = true;
            dwSensor.Contact.Should().Be((DoorWindowContactState)DataToZigbeeResource(data)[0].Value);
        };

        // Act
        dwSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
