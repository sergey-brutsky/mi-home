using System.Collections.Generic;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class XiaomiMultifunctionalGateway2Tests : Gw2DeviceTests
{
    private readonly XiaomiMultifunctionalGateway2 _gateway;
    private const string _gatewaySid = "34ce1188db36";
    private const int _shortId = 0;

    public XiaomiMultifunctionalGateway2Tests()
    {
        _gateway = new XiaomiMultifunctionalGateway2(_gatewaySid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_Gateway_Hearbeat_Data()
    {
        // Arrange        
        var cmd = CreateCommand("heartbeat", "gateway", _gatewaySid, 0,
                new Dictionary<string, object>
                {
                    { "ip", "192.168.1.1" }
                });

        // Act
        _gateway.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal("gateway", XiaomiMultifunctionalGateway2.MODEL);
        Assert.Equal("34ce1188db36", _gateway.Sid);
        Assert.Equal("192.168.1.1", _gateway.Ip);
    }

    [Fact]
    public void Check_Gateway_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", "gateway", _gatewaySid, 0,
                new Dictionary<string, object>
                {
                    { "rgb", 0 },
                    { "illumination", 495 }
                });

        // Act
        _gateway.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal("gateway", XiaomiMultifunctionalGateway2.MODEL);
        Assert.Equal("34ce1188db36", _gateway.Sid);
        Assert.Equal(0, _gateway.Rgb);
        Assert.Equal(495, _gateway.Illumination);
    }
}