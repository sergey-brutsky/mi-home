using AutoFixture;
using Xunit;
using FluentAssertions;
using static MiHomeLib.XiaomiGateway3.Devices.XiaomiWindowDoorSensor;
using MiHomeLib.XiaomiGateway3.Devices;
using System.Threading.Tasks;

namespace MiHomeUnitTests.XiaomiGateway3SubDevices;

public class AqaraWindowDoorSensorTests: Gw3DeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnContactChange_Event(string data)
    {
        // Arrange  
        var dwSensor = _fixture
                    .Build<AqaraWindowDoorSensor>()
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
