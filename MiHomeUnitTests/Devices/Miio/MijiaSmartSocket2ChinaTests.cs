using MiHomeUnitTests.Infrastructure;
using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using MiHomeLib;
using System;

namespace MiHomeUnitTests.Devices.Miio;

public class MijiaSmartSocket2ChinaTests : MiioDeviceTestBase
{
    private readonly MijiaSmartSocket2China _socket;

    public MijiaSmartSocket2ChinaTests()
    {
        SetupMiioInfo();

        _socket = new MijiaSmartSocket2China(_miioTransport.Object);
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
        // Act
        var str = _socket.ToString();

        // Assert        
        str.Should().Contain($"Model: {MijiaSmartSocket2China.MARKET_MODEL} {MijiaSmartSocket2China.MODEL}");
    }

    [Fact]
    public void GetPowerState_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 1, true));

        // Act
        var turnedOn = _socket.GetPowerState();

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
        _socket.TurnOn();

        // Assert
        VerifySetProperties(2, 1, true);
    }

    [Fact]
    public void TurnOff_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _socket.TurnOff();

        // Assert
        VerifySetProperties(2, 1, false);
    }

    [Fact]
    public void TogglePower_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _socket.TogglePower();

        // Assert
        VerifySetProperties(4, 6, true);
    }

    [Fact]
    public void Get_Temperature_Should_Return_Correct_State()
    {
        // Arrange
        byte expected = 40;
        SetupGetProperties((2, 6, expected));

        // Act
        var t = _socket.GetTemperature();

        // Assert
        VerifyGetProperties(2, 6);
        t.Should().Be(expected);
    }

    [Fact]
    public void Get_ElectricCurrent_Should_Return_Correct_State()
    {
        // Arrange
        ushort expected = 10;
        SetupGetProperties((5, 2, expected));

        // Act
        var ampere = _socket.GetElectricCurrent();

        // Assert
        VerifyGetProperties(5, 2);
        ampere.Should().Be(expected);
    }

    [Fact]
    public void Get_Voltage_Should_Return_Correct_State()
    {
        // Arrange
        ushort expected = 220;
        SetupGetProperties((5, 3, expected));

        // Act
        var voltage = _socket.GetVoltage();

        // Assert
        VerifyGetProperties(5, 3);
        voltage.Should().Be(expected);
    }

    [Fact]
    public void Get_ElectricPower_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((5, 6, 1500));

        // Act
        var power = _socket.GetElectricPower();

        // Assert
        VerifyGetProperties(5, 6);
        power.Should().Be(15.0f);
    }

    [Fact]
    public void Get_LedEnabled_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((3, 1, false));

        // Act
        var expected = _socket.GetLedEnabled();

        // Assert
        VerifyGetProperties(3, 1);
        expected.Should().BeFalse();
    }

    [Fact]
    public void Set_LedEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _socket.SetLedEnabled(true);

        // Assert
        VerifySetProperties(3, 1, true);
    }

    [Fact]
    public void Get_OverPowerEnabled_Should_Return_Correct_State()
    {
        // Arrange
        SetupGetProperties((7, 1, false));

        // Act
        var expected = _socket.GetOverPowerEnabled();

        // Assert
        VerifyGetProperties(7, 1);
        expected.Should().BeFalse();
    }

    [Fact]
    public void Set_OverPowerEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _socket.SetOverPowerEnabled(true);

        // Assert
        VerifySetProperties(7, 1, true);
    }

    [Fact]
    public void Get_OverPowerThreshold_Should_Return_Correct_Data()
    {
        // Arrange
        SetupGetProperties((7, 2, 2500));

        // Act
        var threshold = _socket.GetOverPowerThreshold();

        // Assert
        VerifyGetProperties(7, 2);
        threshold.Should().Be(2500);
    }

    [Fact]
    public void Set_Correct_OverPowerThreshold_Should_Return_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _socket.SetOverPowerThreshold(2500);

        // Assert
        VerifySetProperties(7, 2, 2500);
    }
    
    [Fact]
    public void Set_InCorrect_OverPowerThreshold_Should_Throw_Exceptions()
    {
        // Arrange
        SetupSetProperties();

        // Act & Assert
        _socket
            .Invoking(x => x.SetOverPowerThreshold(65530))
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }
}
