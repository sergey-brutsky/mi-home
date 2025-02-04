using Xunit;
using AutoFixture;
using FluentAssertions;
using System;
using MiHomeLib.DevicesV3;
using MiHomeLib.Utils;

namespace MiHomeUnitTests.DevicesV3;

public class BleBatteryDeviceTests: MiHome3DeviceTests
{
    [Theory]
    [InlineData(4106, "63", 95)]
    [InlineData(4106, "62", 41)]
    public void Check_OnBatteryPercentChange_Event(int eid, string edata, byte oldBatteryPercent)
    {
        // Arrange       
        var thMonitor2 = _fixture.Build<MiThMonitor2>().Create();
        var eventRaised = false;
        double time = DateTimeOffset.Now.ToUnixTimeSeconds();

        thMonitor2.BatteryPercent = oldBatteryPercent;
        
        thMonitor2.OnBatteryPercentChange += (oldValue) =>
         { 
            eventRaised = oldValue == oldBatteryPercent;
            thMonitor2.BatteryPercent.Should().Be(edata.ToBleByte());
            thMonitor2.LastTimeMessageReceived = time.UnixSecondsToDateTime();
        };
        
        // Act
        thMonitor2.ParseData(SetupBleAsyncEventParams(eid, edata, time).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
}
