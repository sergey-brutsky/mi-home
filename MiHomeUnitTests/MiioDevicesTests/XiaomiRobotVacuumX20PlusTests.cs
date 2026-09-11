using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using MiHomeLib;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiRobotVacuumX20PlusTests : MiioDeviceBase
{
    private readonly XiaomiRobotVacuumX20Plus _miRobot;

    public XiaomiRobotVacuumX20PlusTests()
    {
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("miIO.info"))))
            .Returns(new {
                id = 1,
                result = new
                {
                    uptime = 9683307,
                    life = 9683307,
                    miio_ver = "0.0.9",
                    mac = "dc:dc:13:25:17:34",
                    fw_ver = "4.3.3_0027",
                    hw_ver = "Linux",
                    ap = new
                    {
                        ssid = "ssid1",
                        bssid = "bssid1",
                        rssi = -35,
                        freq = 2437,
                    },
                    netif = new
                    {
                        localIp = "192.168.1.100",
                        mask = "255.255.255.0",
                        gw = "192.168.1.1",
                    }
                }
            }.ToJson());

        _miRobot = new XiaomiRobotVacuumX20Plus(_miioTransport.Object);
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
        // Act
        var str = _miRobot.ToString();

        // Assert        
        str.Should().Contain($"Model: {XiaomiRobotVacuumX20Plus.MARKET_MODEL} {XiaomiRobotVacuumX20Plus.MODEL}");
    }

    [Fact]
    public void StartCleaning_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.StartCleaning();

        // Assert - SIID 2, AIID 1 (start-sweep)
        VerifyCallAction(2, 1);
    }

    [Fact]
    public void StopCleaning_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.StopCleaning();

        // Assert - SIID 4, AIID 2 (stop-clean from vacuum-extend service)
        VerifyCallAction(4, 2);
    }

    [Fact]
    public void Pause_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.Pause();

        // Assert - SIID 2, AIID 2 (stop-sweeping / pause cleanup)
        VerifyCallAction(2, 2);
    }

    [Fact]
    public void GoHome_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.GoHome();

        // Assert - SIID 3, AIID 1 (start-charge from battery service)
        VerifyCallAction(3, 1);
    }

    [Fact]
    public void FindMe_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.FindMe();

        // Assert - SIID 7, AIID 1 (position from audio service)
        VerifyCallAction(7, 1);
    }
}