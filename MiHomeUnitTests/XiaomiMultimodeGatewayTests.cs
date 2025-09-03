using Xunit;
using Moq;
using FluentAssertions;
using MiHomeLib.MultimodeGateway;
using MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
using System;

namespace MiHomeUnitTests;

public class XiaomiMultimodeGatewayTests : MultimodeGatewayDeviceTests
{
    private const string GW_DID = "216264671";
    private readonly Mock<MultimodeGateway> _gw;

    public XiaomiMultimodeGatewayTests()
    {
        _gw = new Mock<MultimodeGateway>(GW_DID, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object);
    }

    [Fact]
    public void Get_LedEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((6, 6, GW_DID, 1));

        // Act
        var ledEnabled = _gw.Object.LedEnabled;

        // Assert
        VerifyGetProperties(6, 6, GW_DID);
        ledEnabled.Should().BeTrue();
    }

    [Fact]
    public void Set_LedEnabled_Should_Work_As_Expected()
    {
        // Arrange
        var expected = 0;
        SetupSetProperties();

        // Act
        _gw.Object.LedEnabled = false;

        // Assert
        VerifySetProperties(6, 6, GW_DID, expected, 2);
    }

    [Fact]
    public void Get_ArmingMode_Returns_Correct_Mode()
    {
        // Arrange
        var expected = MultimodeGateway.ArmingModeValue.Sleep;

        SetupGetProperties((3, 1, GW_DID, expected));

        // Act
        var armingMode = _gw.Object.ArmingMode;

        // Assert
        VerifyGetProperties(3, 1, GW_DID);
        armingMode.Should().Be(expected);
    }

    [Fact]
    public void Set_ArmingMode_Should_Setup_Correct_Mode()
    {
        // Arrange
        var expected = MultimodeGateway.ArmingModeValue.Sleep;
        SetupSetProperties();

        // Act
        _gw.Object.ArmingMode = expected;

        // Assert
        VerifySetProperties(3, 1, GW_DID, (int)expected, 2);
    }

    [Fact]
    public void Get_AlarmState_Returns_Correct_State()
    {
        // Arrange
        var expected = MultimodeGateway.AlarmStateValue.NonSecurityAlarm;

        SetupGetProperties((3, 22, GW_DID, expected));

        // Act
        var alarmState = _gw.Object.AlarmState;

        // Assert
        VerifyGetProperties(3, 22, GW_DID);
        alarmState.Should().Be(expected);
    }

    [Fact]
    public void Set_AlarmState_Should_Setup_Correct_State()
    {
        // Arrange
        var expected = MultimodeGateway.AlarmStateValue.NoAlarm;
        SetupSetProperties();

        // Act
        _gw.Object.AlarmState = expected;

        // Assert
        VerifySetProperties(3, 22, GW_DID, (int)expected, 2);
    }

    [Fact]
    public void Get_DelayForArmingMode_Returns_Correct_Delay()
    {
        // Arrange
        byte expectedDelay = 5;

        SetupGetProperties((3, 18, GW_DID, expectedDelay));

        // Act
        var delay = _gw.Object.GetDelayTimeForArmingMode(MultimodeGateway.ArmingModeValue.Sleep);

        // Assert
        VerifyGetProperties(3, 18, GW_DID);
        delay.Should().Be(expectedDelay);
    }

    [Fact]
    public void Set_DelayForArmingMode_Should_Work_As_Expected()
    {
        // Arrange
        byte expected = 50;
        SetupSetProperties();

        // Act
        _gw.Object.SetDelayTimeForArmingMode(MultimodeGateway.ArmingModeValue.Sleep, expected);

        // Assert
        VerifySetProperties(3, 18, GW_DID, expected, 2);
    }

    [Fact]
    public void Set_Incorrect_DelayForArmingMode_Should_Throw_Exception()
    {
        // Arrange
        byte expected = 65;
        SetupSetProperties();

        // Act
        var result = _gw.Invoking(x => x.Object.SetDelayTimeForArmingMode(MultimodeGateway.ArmingModeValue.Sleep, expected));

        // Assert
        result.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_AlarmDurationForArmingMode_Returns_Correct_Duration()
    {
        // Arrange
        uint expected = 1_000_000;

        SetupGetProperties((3, 19, GW_DID, expected));

        // Act
        var duration = _gw.Object.GetAlarmDurationForArmingMode(MultimodeGateway.ArmingModeValue.Sleep);

        // Assert
        VerifyGetProperties(3, 19, GW_DID);
        duration.Should().Be(expected);
    }

    [Fact]
    public void Set_AlarmDurationForArmingMode_Should_Work_As_Expected()
    {
        // Arrange
        uint expected = 2_000_000;
        SetupSetProperties();

        // Act
        _gw.Object.SetAlarmDurationForArmingMode(MultimodeGateway.ArmingModeValue.Sleep, expected);

        // Assert
        VerifySetProperties(3, 19, GW_DID, expected, 2);
    }

    [Fact]
    public void Set_Incorrect_AlarmDurationForArmingMode_Should_Throw_Exception()
    {
        // Arrange
        uint expected = 3_000_000_000;
        SetupSetProperties();

        // Act
        var result = _gw.Invoking(x => x.Object.SetAlarmDurationForArmingMode(MultimodeGateway.ArmingModeValue.Sleep, expected));

        // Assert
        result
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_AlarmVolumenForArmingMode_Returns_Correct_VolumeLevel()
    {
        // Arrange
        var expected = MultimodeGateway.VolumeLeveValue.Middle;
        SetupGetProperties((3, 20, GW_DID, expected));

        // Act
        var volumeLevel = _gw.Object.GetAlarmVolumeForArmingMode(MultimodeGateway.ArmingModeValue.Sleep);

        // Assert
        VerifyGetProperties(3, 20, GW_DID);
        volumeLevel.Should().Be(expected);
    }

    [Fact]
    public void Set_AlarmVolumenForArmingMode_Should_Work_As_Expected()
    {
        // Arrange
        var expected = MultimodeGateway.VolumeLeveValue.High;
        SetupSetProperties();

        // Act
        _gw.Object.SetAlarmVolumeForArmingMode(MultimodeGateway.ArmingModeValue.Sleep, expected);

        // Assert
        VerifySetProperties(3, 20, GW_DID, expected, 2);
    }
}
