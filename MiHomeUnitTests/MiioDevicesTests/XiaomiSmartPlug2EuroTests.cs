using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using AutoFixture;
using MiHomeLib;
using System;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiSmartPlug2EuroTests : MiioDeviceBase
{
    private readonly XiaomiSmartPlug2Euro _plug;
    private readonly Fixture _fixture;

    public XiaomiSmartPlug2EuroTests()
    {
        _fixture = new Fixture();

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
                    },
                    netif = new
                    {
                        localIp = "192.168.1.100",
                        mask = "255.255.255.0",
                        gw = "192.168.1.1",
                    }
                }
            }.ToJson());

        _plug = new XiaomiSmartPlug2Euro(_miioTransport.Object);
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
        // Act
        var str = _plug.ToString();

        // Assert        
        str.Should().Contain($"Model: {XiaomiSmartPlug2Euro.MARKET_MODEL} {XiaomiSmartPlug2Euro.MODEL}");
    }

    [Fact]
    public void IsTurnedOn_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 1, true));

        // Act
        var turnedOn = _plug.IsTurnedOn;

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
        _plug.TurnOn();

        // Assert
        VerifySetProperties(2, 1, true);
    }

    [Fact]
    public void TurnOff_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.TurnOff();

        // Assert
        VerifySetProperties(2, 1, false);
    }

    [Fact]
    public void TogglePower_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _plug.TogglePower();

        // Assert
        VerifyCallAction(2, 1);
    }

    [Fact]
    public void Get_DefaultPowerOnState_Should_Return_Correct_State()
    {
        // Arrange
        var expected = (byte)(_fixture.Create<byte>() % 3);
        SetupGetProperties((2, 2, expected));

        // Act
        var defaultPowerOnState = _plug.DefaultPowerOnState;

        // Assert
        VerifyGetProperties(2, 2);
        ((byte)defaultPowerOnState).Should().Be(expected);
    }

    [Fact]
    public void Set_DefaultPowerOnState_Should_Work_As_Expected()
    {
        // Arrange
        var expected = XiaomiSmartPlug2Euro.PowerOnState.TurnOffPower;
        SetupSetProperties();

        // Act
        _plug.DefaultPowerOnState = expected;

        // Assert
        VerifySetProperties(2, 2, (byte)expected);
    }

    [Fact]
    public void Get_DeviceFaultState_Should_Return_Correct_State()
    {
        // Arrange
        var expected = (byte)(_fixture.Create<byte>() % 3);
        SetupGetProperties((2, 3, expected));

        // Act
        var deviceFaultState = _plug.DeviceFaultState;

        // Assert
        VerifyGetProperties(2, 3);
        ((byte)deviceFaultState).Should().Be(expected);
    }

    [Fact]
    public void Get_ChargingProtectionEnabled_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((4, 1, false));

        // Act
        var state = _plug.ChargingProtectionEnabled;

        // Assert
        VerifyGetProperties(4, 1);
        state.Should().BeFalse();
    }

    [Fact]
    public void Set_ChargingProtectionEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.ChargingProtectionEnabled = true;

        // Assert
        VerifySetProperties(4, 1, true);
    }

    [Fact]
    public void Get_ChargingProtectionPowerThreshold_Should_Return_Correct_State()
    {
        // Arrange
        ushort expected = 10;
        SetupGetProperties((4, 2, expected));

        // Act
        var threshold = _plug.ChargingProtectionPowerThreshold;

        // Assert
        VerifyGetProperties(4, 2);
        threshold.Should().Be(expected);
    }

    [Fact]
    public void Set_ChargingProtectionPowerThreshold_Should_Work_As_Expected()
    {
        // Arrange
        ushort expected = 20;
        SetupSetProperties();

        // Act
        _plug.ChargingProtectionPowerThreshold = expected;

        // Assert
        VerifySetProperties(4, 2, expected);
    }

    [Fact]
    public void Set_Incorrect_ChargingProtectionPowerThreshold_Should_Throw_Exception()
    {
        // Arrange
        ushort expected = 1500;
        SetupSetProperties();

        // Act & Assert
        _plug
            .Invoking(x => x.ChargingProtectionPowerThreshold = expected)
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_ChargingProtectionTime_Should_Return_Correct_State()
    {
        // Arrange
        ushort expected = 10;
        SetupGetProperties((4, 3, expected));

        // Act
        var time = _plug.ChargingProtectionTime;

        // Assert
        VerifyGetProperties(4, 3);
        time.Should().Be(expected);
    }

    [Fact]
    public void Set_ChargingProtectionTime_Should_Work_As_Expected()
    {
        // Arrange
        ushort expected = 20;
        SetupSetProperties();

        // Act
        _plug.ChargingProtectionTime = expected;

        // Assert
        VerifySetProperties(4, 3, expected);
    }

    [Fact]
    public void Set_Incorrect_ChargingProtectionTime_Should_Throw_Exception()
    {
        // Arrange
        ushort expected = 0;
        SetupSetProperties();

        // Act & Assert
        _plug
            .Invoking(x => x.ChargingProtectionTime = expected)
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_CyclingEnabled_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((5, 1, false));

        // Act
        var cycling = _plug.CyclingEnabled;

        // Assert
        VerifyGetProperties(5, 1);
        cycling.Should().BeFalse();
    }

    [Fact]
    public void Set_CyclingEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.CyclingEnabled = true;

        // Assert
        VerifySetProperties(5, 1, true);
    }

    [Fact]
    public void Get_CyclingData_Should_Return_Correct_Data()
    {
        // Arrange
        SetupGetProperties((5, 2, "2;3;0;5"));

        // Act
        var (minutesOn, minutesOff, cycleState, cyclesCount) = _plug.CycleData;

        // Assert
        VerifyGetProperties(5, 2);
        minutesOn.Should().Be(2);
        minutesOff.Should().Be(3);
        cycleState.Should().Be(XiaomiSmartPlug2Euro.CycleState.TurnOff);
        cyclesCount.Should().Be(5);
    }

    [Fact]
    public void Set_CyclingData_Should_Return_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.CycleData = (10, 20, XiaomiSmartPlug2Euro.CycleState.RestoreLast, 10);

        // Assert
        VerifySetProperties(5, 2, string.Empty, "10;20;2;10");
    }

    [Fact]
    public void Set_Incorrect_CyclingData_Should_Throw_Exception()
    {
        // Arrange
        SetupSetProperties();

        // Act & Assert
        _plug
            .Invoking(x => x.CycleData = (0, 20, XiaomiSmartPlug2Euro.CycleState.RestoreLast, 10))
            .Should()
            .Throw<ArgumentOutOfRangeException>();

        _plug
            .Invoking(x => x.CycleData = (10, 0, XiaomiSmartPlug2Euro.CycleState.RestoreLast, 10))
            .Should()
            .Throw<ArgumentOutOfRangeException>();

        _plug
            .Invoking(x => x.CycleData = (10, 20, XiaomiSmartPlug2Euro.CycleState.RestoreLast, 11))
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_PhysicalControlBlocked_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((7, 1, false));

        // Act
        var lockState = _plug.PhysicalControlBlocked;

        // Assert
        VerifyGetProperties(7, 1);
        lockState.Should().BeFalse();
    }

    [Fact]
    public void Set_PhysicalControlBlocked_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.PhysicalControlBlocked = true;

        // Assert
        VerifySetProperties(7, 1, true);
    }

    [Fact]
    public void Get_MaxPowerLimitEnabled_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((9, 1, false));

        // Act
        var expected = _plug.MaxPowerLimitEnabled;

        // Assert
        VerifyGetProperties(9, 1);
        expected.Should().BeFalse();
    }

    [Fact]
    public void Set_MaxPowerLimitEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.MaxPowerLimitEnabled = true;

        // Assert
        VerifySetProperties(9, 1, true);
    }

    [Fact]
    public void Get_MaxPowerThreshold_Should_Return_Correct_Data()
    {
        // Arrange
        SetupGetProperties((9, 2, 2000));

        // Act
        var threshold = _plug.MaxPowerThreshold;

        // Assert
        VerifyGetProperties(9, 2);
        threshold.Should().Be(2000);
    }

    [Fact]
    public void Set_MaxPowerThreshold_Should_Return_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.MaxPowerThreshold = 3500;

        // Assert
        VerifySetProperties(9, 2, 3500);
    }

    [Fact]
    public void Set_Incorrect_MaxPowerThreshold_Should_Throw_Exception()
    {
        // Arrange
        SetupSetProperties();

        // Act & Assert
        _plug
            .Invoking(x => x.MaxPowerThreshold = 200)
            .Should()
            .Throw<ArgumentOutOfRangeException>();

        _plug
            .Invoking(x => x.MaxPowerThreshold = 4000)
            .Should()
            .Throw<ArgumentOutOfRangeException>();

        _plug
            .Invoking(x => x.MaxPowerThreshold = 350)
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_CurrentElectricPower_Should_Return_Correct_Data()
    {
        // Arrange
        SetupGetProperties((11, 2, 300));

        // Act
        var watts = _plug.CurrentElectricPower;

        // Assert
        VerifyGetProperties(11, 2);
        watts.Should().Be(300);
    }

    [Fact]
    public void Get_LedEnabled_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((13, 1, false));

        // Act
        var expected = _plug.LedEnabled;

        // Assert
        VerifyGetProperties(13, 1);
        expected.Should().BeFalse();
    }

    [Fact]
    public void Set_LedEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _plug.LedEnabled = true;

        // Assert
        VerifySetProperties(13, 1, true);
    }    
}
