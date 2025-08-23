using Xunit;
using AutoFixture;
using FluentAssertions;
using System;
using MiHomeLib;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class BleBatteryDeviceTests: MultimodeGatewayDeviceTests
{
    [Theory]
    [InlineData(4106, "63", 95)]
    [InlineData(4106, "62", 41)]
    public void Check_OnBatteryPercentChange_Event(int eid, string edata, byte oldBatteryPercent)
    {
        // Arrange       
        var thMonitor2 = _fixture.Build<XiaomiBluetoothHygrothermograph2>().Create();
        var eventRaised = false;
        double time = DateTimeOffset.Now.ToUnixTimeSeconds();

        thMonitor2.BatteryPercent = oldBatteryPercent;
        
        thMonitor2.OnBatteryPercentChangeAsync += (oldValue) =>
         { 
            eventRaised = oldValue == oldBatteryPercent;
            thMonitor2.BatteryPercent.Should().Be(thMonitor2.ToBleByte(edata));
            thMonitor2.LastTimeMessageReceived = time.UnixSecondsToDateTime();
            return Task.CompletedTask;
        };
        
        // Act
        thMonitor2.ParseData(SetupBleAsyncEventParams(eid, edata, time).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
