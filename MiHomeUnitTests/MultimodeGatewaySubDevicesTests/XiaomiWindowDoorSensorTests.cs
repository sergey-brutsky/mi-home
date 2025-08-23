using AutoFixture;
using Xunit;
using FluentAssertions;
using static MiHomeLib.MultimodeGateway.Devices.XiaomiWindowDoorSensor;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class XiaomiWindowDoorSensorTests: MultimodeGatewayDeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnContactChange_Event(string data)
    {
        // Arrange  
        var dwSensor = _fixture
                    .Build<XiaomiWindowDoorSensor>()
                    .Create();
        
        var eventRaised = false;

        dwSensor.OnContactChangedAsync += () => 
        { 
            eventRaised = true;
            dwSensor.Contact.Should().Be((DoorWindowContactState)DataToZigbeeResource(data)[0].Value);
            return Task.CompletedTask;
        };

        // Act
        dwSensor.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
