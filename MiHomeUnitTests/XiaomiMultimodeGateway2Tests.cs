using Xunit;
using Moq;
using FluentAssertions;
using MiHomeLib.MultimodeGateway;
using MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
using System;

namespace MiHomeUnitTests;

public class XiaomiMultimodeGateway2Tests : MultimodeGatewayDeviceTests
{
    private const string GW_DID = "397569381";
    private readonly Mock<MultimodeGateway2> _gw;

    public XiaomiMultimodeGateway2Tests()
    {
        _gw = new Mock<MultimodeGateway2>(GW_DID, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object);
    }

    [Fact]
    public void Get_AccessMode_Returns_Correct_Network_Mode()
    {
        // Arrange
        var expected = MultimodeGateway2.AccessModeValue.Wireless2G;

        SetupGetProperties((2, 1, GW_DID, expected));

        // Act
        var accessMode = _gw.Object.AccessMode;

        // Assert
        VerifyGetProperties(2, 1, GW_DID);
        accessMode.Should().Be(expected);
    }

    [Fact]
    public void Set_AccessMode_Should_Setup_Correct_Access_Mode()
    {
        // Arrange
        var expected = MultimodeGateway2.AccessModeValue.Wireless5G;
        SetupSetProperties();

        // Act
        _gw.Object.AccessMode = expected;

        // Assert
        VerifySetProperties(2, 1, GW_DID, (int)expected, 2);
    }

    [Fact]
    public void Get_DoNotDisturbModeEnabled_Returns_Correct_DoNotDisturbMode_Mode()
    {
        // Arrange
        SetupGetProperties((6, 1, GW_DID, 0));

        // Act
        var enabled = _gw.Object.DoNotDisturbModeEnabled;

        // Assert
        VerifyGetProperties(6, 1, GW_DID);
        enabled.Should().BeFalse();
    }

    [Fact]
    public void Set_DoNotDisturbModeEnabled_Should_Setup_Correct_DoNotDisturb_Mode()
    {
        // Arrange
        var expected = 1;
        SetupSetProperties();

        // Act
        _gw.Object.DoNotDisturbModeEnabled = true;

        // Assert
        VerifySetProperties(6, 1, GW_DID, expected, 2);
    }

    [Fact]
    public void Get_DoNotDisturbEffectiveTime_Returns_Correct_DoNotDisturbEffectiveTime()
    {
        // Arrange
        var (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute) = ((ushort)23, (ushort)05, (ushort)08, (ushort)10);
        var str = $"{expectedStartHour:D2}:{exectedStartMinute:D2}-{expectedEndHour:D2}:{expectedEndMinute:D2}";

        SetupGetProperties((6, 2, GW_DID, str));

        // Act
        var (startHour, startMinute, endHour, endMinute) = _gw.Object.DoNotDisturbEffectiveTime;

        // Assert
        VerifyGetProperties(6, 2, GW_DID);
        startHour.Should().Be(expectedStartHour);
        startMinute.Should().Be(exectedStartMinute);
        endHour.Should().Be(expectedEndHour);
        endMinute.Should().Be(expectedEndMinute);
    }

    [Fact]
    public void Set_Correct_DoNotDisturb_EffectiveTime_Should_Work_As_Expected()
    {
        // Arrange
        var (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute) = ((ushort)23, (ushort)05, (ushort)08, (ushort)10);
        var expected = $"{expectedStartHour:D2}:{exectedStartMinute:D2}-{expectedEndHour:D2}:{expectedEndMinute:D2}"; ;
        SetupSetProperties();

        // Act
        _gw.Object.DoNotDisturbEffectiveTime = (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute);

        // Assert
        VerifySetProperties(6, 2, GW_DID, expected, 2);
    }

    [Theory]
    [InlineData(25, 05, 08, 10)]
    [InlineData(23, 60, 08, 10)]
    [InlineData(23, 05, 30, 10)]
    [InlineData(23, 05, 08, 65)]
    public void Set_InCorrect_EffectiveTime_Should_Throw_Exception(ushort startHour, ushort startMinute, ushort endHour, ushort endMinute)
    {
        // Act
        _gw
            .Object
            .Invoking(x => x.DoNotDisturbEffectiveTime = (startHour, startMinute, endHour, endMinute))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_Led_Brightness_Should_Return_Correct_Brightness_Value()
    {
        // Arrange
        ushort expected = 55;

        SetupGetProperties((6, 3, GW_DID, expected));

        // Act
        var ledBrightness = _gw.Object.LedIndicatorBrightness;

        // Assert
        VerifyGetProperties(6, 3, GW_DID);
        ledBrightness.Should().Be(expected);
    }

    [Fact]
    public void Set_Correct_Led_Brightness_Should_Work_As_Expected()
    {
        // Arrange
        ushort expected = 60;
        SetupSetProperties();

        // Act
        _gw.Object.LedIndicatorBrightness = expected;

        // Assert
        VerifySetProperties(6, 3, GW_DID, expected, 2);
    }

    [Fact]
    public void Set_Incorrect_Led_Brightness_Should_Throw_Exceptions()
    {
        // Act
        _gw
            .Object
            .Invoking(x => x.LedIndicatorBrightness = 150)
            .Should().Throw<ArgumentOutOfRangeException>();
    }
    
    [Fact]
    public void Get_AccidentalDeletionEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((7, 1, GW_DID, 1));

        // Act
        var accidentalDeletion = _gw.Object.AccidentalDeletionEnabled;

        // Assert
        VerifyGetProperties(7, 1, GW_DID);
        accidentalDeletion.Should().BeTrue();
    }

    [Fact]
    public void Set_Accidental_Deletion_Should_Work_As_Expected()
    {
        // Arrange
        var expected = 0;
        SetupSetProperties();

        // Act
        _gw.Object.AccidentalDeletionEnabled = false;

        // Assert
        VerifySetProperties(7, 1, GW_DID, expected, 2);
    }
}
