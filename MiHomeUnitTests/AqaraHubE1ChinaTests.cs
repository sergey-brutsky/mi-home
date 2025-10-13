using Xunit;
using FluentAssertions;
using MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
using MiHomeLib.MqttGateway;
using System;
using AutoFixture;

namespace MiHomeUnitTests;

public class AqaraHubE1ChinaTests : MqttGatewayDeviceTests
{
    private const string GW_DID = "1111111111";
    private readonly AqaraHubE1China _gw;

    public AqaraHubE1ChinaTests()
    {
        _gw = new AqaraHubE1China(GW_DID, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object);
    }

    [Fact]
    public void Get_LedEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((6, 1, GW_DID, 1));

        // Act
        var ledEnabled = _gw.NightModeEnabled;

        // Assert
        VerifyGetProperties(6, 1, GW_DID);
        ledEnabled.Should().BeTrue();
    }

    [Fact]
    public void Set_LedEnabled_Should_Work_As_Expected()
    {
        // Arrange
        var expected = 0;
        SetupSetProperties();

        // Act
        _gw.NightModeEnabled = false;

        // Assert
        VerifySetProperties(6, 1, GW_DID, expected, 2);
    }

    [Fact]
    public void Get_NightModeEffectiveTime_Returns_Correct_DoNotDisturbEffectiveTime()
    {
        // Arrange
        var (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute) = ((byte)23, (byte)05, (byte)08, (byte)10);
        var str = $"{expectedStartHour:D2}:{exectedStartMinute:D2}-{expectedEndHour:D2}:{expectedEndMinute:D2}";

        SetupGetProperties((6, 2, GW_DID, str));

        // Act
        var (startHour, startMinute, endHour, endMinute) = _gw.NigtModeEffectiveHours;

        // Assert
        VerifyGetProperties(6, 2, GW_DID);
        startHour.Should().Be(expectedStartHour);
        startMinute.Should().Be(exectedStartMinute);
        endHour.Should().Be(expectedEndHour);
        endMinute.Should().Be(expectedEndMinute);
    }

    [Fact]
    public void Set_Correct_NightMode_EffectiveTime_Should_Work_As_Expected()
    {
        // Arrange
        var (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute) = ((byte)23, (byte)05, (byte)08, (byte)10);
        var expected = $"{expectedStartHour:D2}:{exectedStartMinute:D2}-{expectedEndHour:D2}:{expectedEndMinute:D2}"; ;
        SetupSetProperties();

        // Act
        _gw.NigtModeEffectiveHours = (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute);

        // Assert
        VerifySetProperties(6, 2, GW_DID, expected, 2);
    }

    [Theory]
    [InlineData(25, 05, 08, 10)]
    [InlineData(23, 60, 08, 10)]
    [InlineData(23, 05, 30, 10)]
    [InlineData(23, 05, 08, 65)]
    public void Set_InCorrect_EffectiveTime_Should_Throw_Exception(byte startHour, byte startMinute, byte endHour, byte endMinute)
    {
        // Act
        _gw
            .Invoking(x => x.NigtModeEffectiveHours = (startHour, startMinute, endHour, endMinute))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_HotspotModeEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((19, 1, GW_DID, true));

        // Act
        var hotspotEnabled = _gw.HotspotModeEnabled;

        // Assert
        VerifyGetProperties(19, 1, GW_DID);
        hotspotEnabled.Should().BeTrue();
    }

    [Fact]
    public void Set_HotspotModeEnabled_Should_Work_As_Expected()
    {
        // Arrange
        var expected = false;
        SetupSetProperties();

        // Act
        _gw.HotspotModeEnabled = false;

        // Assert
        VerifySetProperties(19, 1, GW_DID, expected, 2);
    }

    [Fact]
    public void Get_HotspotName_Should_Return_Correct_Value()
    {
        // Arrange
        string expected = "hotspotname";

        SetupGetProperties((19, 2, GW_DID, expected));

        // Act
        var hotspotName = _gw.HotspotName;

        // Assert
        VerifyGetProperties(19, 2, GW_DID);
        hotspotName.Should().Be(expected);
    }

    [Fact]
    public void Set_Correct_HotspotName_Should_Work_As_Expected()
    {
        // Arrange
        var expected = _fixture.Create<string>()[..10];
        SetupSetProperties();

        // Act
        _gw.HotspotName = expected;

        // Assert
        VerifySetProperties(19, 2, GW_DID, expected, 2);
    }

    [Fact]
    public void Set_Incorrect_HotspotName_Should_Throw_Exception()
    {
        // Arrange
        var expected = _fixture.Create<string>()[..36]; // not valid length
        SetupSetProperties();

        // Act & Assert
        _gw
            .Invoking(x => x.HotspotName = expected)
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_HotspotPassword_Should_Return_Correct_Value()
    {
        // Arrange
        string expected = _fixture.Create<string>();

        SetupGetProperties((19, 3, GW_DID, expected));

        // Act
        var hotspotPassword = _gw.HotspotPassword;

        // Assert
        VerifyGetProperties(19, 3, GW_DID);
        hotspotPassword.Should().Be(expected);
    }

    [Fact]
    public void Set_Correct_HotspotPassword_Should_Work_As_Expected()
    {
        // Arrange
        var expected = _fixture.Create<string>();
        SetupSetProperties();

        // Act
        _gw.HotspotPassword = expected;

        // Assert
        VerifySetProperties(19, 3, GW_DID, expected, 2);
    }

    [Fact]
    public void Set_Incorrect_HotspotPassword_Should_Throw_Exception()
    {
        // Arrange
        var expected = string.Join(string.Empty, _fixture.Create<string>(), _fixture.Create<string>());

        SetupSetProperties();

        // Act & Assert
        _gw
            .Invoking(x => x.HotspotPassword = expected)
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_HotspotClients_Should_Return_Correct_Value()
    {
        // Arrange
        var expected = "[{'ip':'192.168.189.8','mac':'58:85:a2:00:e4:9b','name':'client1'},{'ip':'192.168.189.7','mac':'58:85:a2:00:e4:9a','name':'client2'}]";

        SetupGetProperties((19, 4, GW_DID, expected));

        // Act
        var hotspotClients = _gw.HotspotClients;

        // Assert
        VerifyGetProperties(19, 4, GW_DID);
        hotspotClients[0].name.Should().Be("client1");
        hotspotClients[1].name.Should().Be("client2");
    }
}
