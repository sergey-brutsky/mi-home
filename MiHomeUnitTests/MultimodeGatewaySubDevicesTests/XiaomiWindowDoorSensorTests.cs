using System;
using AutoFixture;
using Xunit;
using FluentAssertions;
using static MiHomeLib.MqttGateway.Devices.XiaomiWindowDoorSensor;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

/// <summary>
/// Covers AqaraWindowDoorSensor too: it inherits the parsing of XiaomiWindowDoorSensor
/// </summary>
public class XiaomiWindowDoorSensorTests: MqttGatewayDeviceTests
{
    [Theory]
    [InlineData(typeof(XiaomiWindowDoorSensor), 1, DoorWindowContactState.Opened)]
    [InlineData(typeof(XiaomiWindowDoorSensor), 0, DoorWindowContactState.Closed)]
    [InlineData(typeof(AqaraWindowDoorSensor), 1, DoorWindowContactState.Opened)]
    [InlineData(typeof(AqaraWindowDoorSensor), 0, DoorWindowContactState.Closed)]
    public void Check_OnContactChange_Event(Type sensorType, int value, DoorWindowContactState expected)
    {
        // Arrange  
        var dwSensor = (XiaomiWindowDoorSensor)Activator.CreateInstance(sensorType, _fixture.Create<string>(), _loggerFactory);
        var eventRaised = false;

        dwSensor.OnContactChangedAsync += () => 
        { 
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        dwSensor.ParseData($"[{{\"res_name\":\"3.1.85\",\"value\":{value}}}]");

        // Assert
        eventRaised.Should().BeTrue();
        dwSensor.Contact.Should().Be(expected);
    }
}
