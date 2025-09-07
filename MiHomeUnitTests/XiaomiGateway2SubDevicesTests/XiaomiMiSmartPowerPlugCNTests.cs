using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiMiSmartPowerPlugCNTests : Gw2DeviceTests
{
    private readonly string _sid = "158d00015dc332";
    private readonly string _gwPassword = "123456789";
    private readonly int _shortId = 10624;
    private readonly XiaomiMiSmartPowerPlugCN _socketPlug;

    public XiaomiMiSmartPowerPlugCNTests()
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        _socketPlug = new XiaomiMiSmartPowerPlugCN(_sid, _shortId, _messageTransport.Object, _gwPassword, _loggerFactory);
    }

    [Fact]
    public void Check_SocketPlug_Hearbeat_Data()
    {
        // Arrange
        var cmd = CreateCommand("heartbeat", XiaomiMiSmartPowerPlugCN.MODEL, _sid, 18916,
                new Dictionary<string, object>
                {
                    { "voltage", 3600 },
                    { "status", "on" },
                    { "inuse", "1" },
                    { "power_consumed", "39009" },
                    { "load_power", "3.20" },
                });

        // Act
        _socketPlug.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _socketPlug.Sid);
        Assert.Equal(3.6f, _socketPlug.Voltage);
        Assert.Equal("on", _socketPlug.Status);
        Assert.Equal(1, _socketPlug.Inuse);
        Assert.Equal(39009, _socketPlug.PowerConsumed);
        Assert.Equal(3.2f, _socketPlug.LoadPower);
    }

    [Fact]
    public void Check_SocketPlug_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", XiaomiMiSmartPowerPlugCN.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "status", "off" }
                });

        // Act
        _socketPlug.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _socketPlug.Sid);
        Assert.Equal("off", _socketPlug.Status);
    }

    [Fact]
    public void Check_SocketPlug_TurnOn_Command()
    {
        // Arrange
        var command = JsonSerializer.Serialize(new { status = "on" });
        
        // Act
        _socketPlug.TurnOn();
        
        // Assert
        _messageTransport.Verify(x => x
            .SendWriteCommand(_sid, XiaomiMiSmartPowerPlugCN.MODEL, _gwPassword,
                It.Is<SocketPlugCommand>(c => c.ToString() == command)), Times.Once());
    }

    [Fact]
    public void Check_SocketPlug_TurnOff_Command()
    {
        // Arrange
        var command = JsonSerializer.Serialize(new { status = "off" });
        
        // Act
        _socketPlug.TurnOff();

        // Assert
        _messageTransport.Verify(x => x
            .SendWriteCommand(_sid, XiaomiMiSmartPowerPlugCN.MODEL, _gwPassword,
                It.Is<SocketPlugCommand>(c => c.ToString() == command)), Times.Once());
    }
}
