using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiWindowDoorSensorTests: Gw2DeviceTests
{
    private readonly XiaomiWindowDoorSensor _device;

    public XiaomiWindowDoorSensorTests()
    {
        _device = new XiaomiWindowDoorSensor(_fixture.Create<string>(), _fixture.Create<byte>(), _loggerFactory);
    }

    private static readonly Dictionary<string, Action<XiaomiWindowDoorSensor, Func<Task>>> Subscribe = new()
    {
        ["open"] = (d, h) => d.OnOpenAsync += h,
        ["close"] = (d, h) => d.OnCloseAsync += h,
        ["no_close_60"] = (d, h) => d.NotClosedFor1MinuteAsync += h,
        ["no_close_300"] = (d, h) => d.NotClosedFor5MinutesAsync += h,
    };

    [Theory]
    [InlineData("open", "{\"status\":\"open\"}")]
    [InlineData("close", "{\"status\":\"close\"}")]
    [InlineData("no_close_60", "{\"no_close\":\"60\"}")]
    [InlineData("no_close_300", "{\"no_close\":\"300\"}")]
    public void Report_Raises_Matching_Event(string @event, string data)
    {
        // Arrange
        var eventRaised = false;
        Subscribe[@event](_device, () => { eventRaised = true; return Task.CompletedTask; });

        // Act
        _device.ParseData(data);

        // Assert
        Assert.True(eventRaised);
    }
}
