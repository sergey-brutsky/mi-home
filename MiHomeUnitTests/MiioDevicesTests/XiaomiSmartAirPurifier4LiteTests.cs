using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using MiHomeLib;
using System;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiSmartAirPurifier4LiteTests : MiioDeviceBase
{
    private readonly XiaomiSmartAirPurifier4Lite _purifier;

    public XiaomiSmartAirPurifier4LiteTests()
    {
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("miIO.info"))))
            .Returns(new
            {
                id = 1,
                result = new
                {
                    life = 8172,
                    miio_ver = "0.1.0",
                    mac = "E8:2A:14:3A:26:1F",
                    fw_ver = "1.0.6",
                    hw_ver = "ESP32C3",
                    ap = new
                    {
                        ssid = "ssid1",
                        bssid = "bssid1",
                        rssi = -35,
                        primary = 2,
                        freq = 2410,
                    },
                    netif = new
                    {
                        localIp = "192.168.1.100",
                        mask = "255.255.255.0",
                        gw = "192.168.1.1",
                    }
                }
            }.ToJson());

        _purifier = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object);
    }

    [Fact]
    public void IsTurnedOn_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 1, true));

        // Act
        var turnedOn = _purifier.IsTurnedOn;

        // Assert
        VerifyGetProperties(2, 1, "2-1");
        turnedOn.Should().BeTrue();
    }

    [Fact]
    public void TurnOn_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.TurnOn();

        // Assert
        VerifySetProperties(2, 1, true);
    }

    [Fact]
    public void TurnOff_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.TurnOff();

        // Assert
        VerifySetProperties(2, 1, false);
    }

    [Fact]
    public void TogglePower_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _purifier.TogglePower();

        // Assert
        VerifyCallAction(2, 1);
    }

    [Fact]
    public void Get_DeviceFaultState_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 2, (byte)0));

        // Act
        var faultState = _purifier.DeviceFaultState;

        // Assert
        VerifyGetProperties(2, 2);
        faultState.Should().Be(FaultState.NoFaults);
    }

    [Fact]
    public void Get_Mode_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 4, (byte)0));

        // Act
        var mode = _purifier.Mode;

        // Assert
        VerifyGetProperties(2, 4);
        mode.Should().Be(OperationMode.Auto);
    }

    [Fact]
    public void Set_Mode_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.Mode = OperationMode.Sleep;

        // Assert
        VerifySetProperties(2, 4, OperationMode.Sleep);
    }

    [Fact]
    public void Get_Humidity_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 1, (byte)55));

        // Act
        var humidity = _purifier.Humidity;

        // Assert
        VerifyGetProperties(3, 1);
        humidity.Should().Be(55);
    }

    [Fact]
    public void Get_Pm25Density_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 4, (ushort)23));

        // Act
        var pm25 = _purifier.Pm25Density;

        // Assert
        VerifyGetProperties(3, 4);
        pm25.Should().Be(23);
    }

    [Fact]
    public void Get_Temperature_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 7, 22.5f));

        // Act
        var temperature = _purifier.Temperature;

        // Assert
        VerifyGetProperties(3, 7);
        temperature.Should().Be(22.5f);
    }

    [Fact]
    public void Get_AirQuality_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 8, (byte)0));

        // Act
        var airQuality = _purifier.AirQuality;

        // Assert
        VerifyGetProperties(3, 8);
        airQuality.Should().Be(AirQuality.Excellent);
    }

    [Fact]
    public void Get_FilterLifeLevel_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((4, 1, (byte)85));

        // Act
        var filterLifeLevel = _purifier.FilterLifeLevel;

        // Assert
        VerifyGetProperties(4, 1);
        filterLifeLevel.Should().Be(85);
    }

    [Fact]
    public void Get_FilterUsedTime_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((4, 3, (ushort)1200));

        // Act
        var filterUsedTime = _purifier.FilterUsedTime;

        // Assert
        VerifyGetProperties(4, 3);
        filterUsedTime.Should().Be(1200);
    }

    [Fact]
    public void Get_FilterLeftTime_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((4, 4, (ushort)180));

        // Act
        var filterLeftTime = _purifier.FilterLeftTime;

        // Assert
        VerifyGetProperties(4, 4);
        filterLeftTime.Should().Be(180);
    }

    [Fact]
    public void ResetFilterLife_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _purifier.ResetFilterLife();

        // Assert
        VerifyCallAction(4, 1);
    }

    [Fact]
    public void Get_BuzzerEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((6, 1, true));

        // Act
        var buzzerEnabled = _purifier.BuzzerEnabled;

        // Assert
        VerifyGetProperties(6, 1);
        buzzerEnabled.Should().BeTrue();
    }

    [Fact]
    public void Set_BuzzerEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.BuzzerEnabled = false;

        // Assert
        VerifySetProperties(6, 1, false);
    }

    [Fact]
    public void Get_ScreenBrightness_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((7, 2, (byte)1));

        // Act
        var screenBrightness = _purifier.ScreenBrightness;

        // Assert
        VerifyGetProperties(7, 2);
        screenBrightness.Should().Be(ScreenBrightness.Bright);
    }

    [Fact]
    public void Set_ScreenBrightness_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.ScreenBrightness = ScreenBrightness.Off;

        // Assert
        VerifySetProperties(7, 2, ScreenBrightness.Off);
    }

    [Fact]
    public void Get_ChildLockEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((8, 1, false));

        // Act
        var childLockEnabled = _purifier.ChildLockEnabled;

        // Assert
        VerifyGetProperties(8, 1);
        childLockEnabled.Should().BeFalse();
    }

    [Fact]
    public void Set_ChildLockEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.ChildLockEnabled = true;

        // Assert
        VerifySetProperties(8, 1, true);
    }

    [Fact]
    public void Get_MotorSpeedRpm_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((9, 1, (ushort)800));

        // Act
        var motorSpeedRpm = _purifier.MotorSpeedRpm;

        // Assert
        VerifyGetProperties(9, 1);
        motorSpeedRpm.Should().Be(800);
    }

    [Fact]
    public void ToggleMode_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _purifier.ToggleMode();

        // Assert
        VerifyCallAction(9, 1);
    }

    [Fact]
    public void Get_FanLevel_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((11, 1, (byte)5));

        // Act
        var fanLevel = _purifier.FanLevel;

        // Assert
        VerifyGetProperties(11, 1);
        fanLevel.Should().Be(5);
    }

    [Fact]
    public void Set_FanLevel_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.FanLevel = 10;

        // Assert
        VerifySetProperties(11, 1, (byte)10);
    }

    [Fact]
    public void Set_FanLevel_Above_Range_Should_Throw_Exception()
    {
        // Act & Assert
        _purifier
            .Invoking(x => x.FanLevel = 15)
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }
}