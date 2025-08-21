using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using System.Collections.Generic;
using AutoFixture;
using System;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiRobotVacuumMop3CTests : MiioDeviceBase
{
    private readonly XiaomiRobotVacuumMop3C _miRobot;
    private readonly Fixture _fixture;

    public XiaomiRobotVacuumMop3CTests()
    {
        _fixture = new Fixture();

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("miIO.info"))))
            .Returns(ToJson(new {
                id = 1,
                result = new
                {
                    uptime = 9683307,
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
            }));

        _miRobot = new XiaomiRobotVacuumMop3C(_miioTransport.Object);
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
        // Act
        var str = _miRobot.ToString();

        // Assert        
        str.Should().Contain($"Model: {XiaomiRobotVacuumMop3C.MARKET_MODEL} {XiaomiRobotVacuumMop3C.MODEL}");
    }

    [Fact]
    public void GetBatteryPercent_Show_Return_Correct_Percentage()
    {
        // Arrange
        var expected = (ushort)(_fixture.Create<ushort>() % 100);
        SetupGetProperties((3,1, expected));

        // Act
        var percentage = _miRobot.GetBatteryPercent();

        // Assert
        VerifyGetProperties(3, 1);
        percentage.Should().Be(expected);
    }

    [Fact]
    public void GetStatus_Should_Return_Correct_Status()
    {
        // Arrange
        var expected = (ushort)(_fixture.Create<ushort>() % 9);
        SetupGetProperties((2, 1, expected));

        // Act
        var status = (ushort)_miRobot.GetStatus();

        // Assert
        VerifyGetProperties(2, 1);
        status.Should().Be(expected);
    }

    [Fact]
    public void SetCleaningMode_Should_Work_As_Expected()
    {
        // Arrange
        var expected = XiaomiRobotVacuumMop3C.CleaningMode.SweepAndMop;
        SetupSetProperties();

        // Act
        _miRobot.SetCleaningMode(expected);

        // Assert
        VerifySetProperties(2, 4, (int)expected);
    }

    [Fact]
    public void GetCleaningMode_Should_Return_Correct_Mode()
    {
        // Arrange
        var expected = (ushort)(_fixture.Create<ushort>() % 3);
        SetupGetProperties((2,4,expected));

        // Act
        var cleaningMode = (ushort)_miRobot.GetCleaningMode();

        // Assert
        VerifyGetProperties(2, 4);
        cleaningMode.Should().Be(expected);
    }

    [Fact]
    public void SetSweepType_Should_Work_As_Expected()
    {
        // Arrange
        var expected = XiaomiRobotVacuumMop3C.SweepType.Mop;
        SetupSetProperties();

        // Act
        _miRobot.SetSweepType(expected);

        // Assert
        VerifySetProperties(2, 8, (int)expected);
    }

    [Fact]
    public void GetSweepType_Should_Return_Correct_SweepType()
    {
        // Arrange
        var expected = (ushort)(_fixture.Create<ushort>() % 9);
        SetupGetProperties((2,8,expected));

        // Act
        var sweepType = (ushort)_miRobot.GetSweepType();

        // Assert
        VerifyGetProperties(2, 8);
        sweepType.Should().Be(expected);
    }

    [Fact]
    public void FindMe_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _miRobot.FindMe();

        // Assert
        VerifySetProperties(4, 1, null);
    }

    [Fact]
    public void StartCleaning_Should_Work_As_Expected()
    {
        // Arrange
        var expected = XiaomiRobotVacuumMop3C.CleaningType.SweepAndMop;
        SetupCallAction();

        // Act
        _miRobot.StartCleaning(expected);

        // Assert
        VerifyCallAction(2, (int)expected);
    }

    [Fact]
    public void StopCleaning_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.StopCleaning();

        // Assert
        VerifyCallAction(2, 2);
    }

    [Fact]
    public void GoCharging_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _miRobot.GoCharging();

        // Assert
        VerifyCallAction(3, 1);
    }

    [Fact]
    public void SetVoiceLevel_With_Wrong_Input_Should_Throws_Exception()
    {
        // Arrange
        SetupCallAction();

        // Act
        var call = _miRobot.Invoking(x => x.SetVoiceLevel(25));

        // Assert
        call.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetVoiceLevel_Should_Work_As_Expected()
    {
        // Arrange
        ushort expected = 5;
        SetupSetProperties();

        // Act
        _miRobot.SetVoiceLevel(expected);

        // Assert
        VerifySetProperties(4, 2, expected);
    }

    [Fact]
    public void GetVoiceLevel_Should_Return_Correct_Level()
    {
        // Arrange
        var expected = (ushort)(_fixture.Create<ushort>() % 11);
        SetupGetProperties([(4, 2, expected)]);

        // Act
        var voiceLevel = _miRobot.GetVoiceLevel();

        // Assert
        VerifyGetProperties(4, 2);
        voiceLevel.Should().Be(expected);
    }

    [Fact]
    public void GetNoDisturbingSettings_Should_Return_Correct_Settings()
    {
        // Arrange
        SetupGetProperties((12, 7, new[] { 1, 23, 05, 08, 10 }));

        // Act
        var settings = _miRobot.GetNoDisturbingSettings();

        // Assert
        VerifyGetProperties(12, 7);
        settings.IsNoDisturbingEnabled.Should().BeTrue();
        settings.DndStartingHour.Should().Be(23);
        settings.DndStartingMinute.Should().Be(05);
        settings.DndEndingHour.Should().Be(08);
        settings.DndEndingMinute.Should().Be(10);
    }

    [Fact]
    public void GetSweepConsumablesInfo_Should_Return_Correct_Data()
    {
        // Arrange
        SetupGetProperties(
            (7, 8, 57), 
            (7, 9, 103), 
            (7, 10, 78), 
            (7, 11, 283), 
            (7, 12, 96), 
            (7, 13, 174), 
            (7, 14, 91), 
            (7, 15, 164) 
        );

        // // Act
        var consumablesInfo = _miRobot.GetSweepConsumablesInfo();

        // // Assert
        VerifyGetProperties(2,(7,8),(7,9),(7,10),(7,11),(7,12),(7,13),(7,14),(7,15));

        consumablesInfo.SideBrushLifePercent.Should().Be(57);
        consumablesInfo.SideBrushLifeHours.Should().Be(103);
        consumablesInfo.MainBrushLifePercent.Should().Be(78);
        consumablesInfo.MainBrushLifeHours.Should().Be(283);
        consumablesInfo.HepaFilterLifePercent.Should().Be(96);
        consumablesInfo.HepaFilterLifeHours.Should().Be(174);
        consumablesInfo.MopLifePercent.Should().Be(91);
        consumablesInfo.MopLifeHours.Should().Be(164);
    }
}
