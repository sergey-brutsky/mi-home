using Xunit;
using Moq;
using FluentAssertions;
using MiHomeLib.MultimodeGateway;
using AutoFixture;
using MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
using System;

namespace MiHomeUnitTests;

public class XiaomiMultimodeGateway2Tests : MultimodeGatewayDeviceTests
{
    private readonly string _gwDid = "397569381";
    private readonly Mock<MultimodeGateway2> _gw;

    public XiaomiMultimodeGateway2Tests()
    {
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("miIO.info"))))
            .Returns(ToJson(new
            {
                id = 1,
                result = new
                {
                    uptime = 502175,
                    miio_ver = "0.0.9",
                    mac = "34:EF:44:49:BC:63",
                    fw_ver = "1.0.5_0008",
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

        _gw = new Mock<MultimodeGateway2>(_gwDid, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object);
    }

    [Fact]
    public void Gw_Properties_Returns_Valid_Values()
    {
        // Arrange
        var expectedUptime = _fixture.Create<uint>();
        var expectedMiioVersion = "0.0.9";
        var expectedMac = "34:EF:44:49:BC:63";
        var expectedFirmwareVersion = "1.0.5_0008";
        var expectedHardwareVersion = "Linux";
        var expectedSsid = _fixture.Create<string>();
        var expectedBssid = _fixture.Create<string>();
        var expectedRssi = _fixture.Create<int>();
        var expectedFreq = _fixture.Create<int>();
        var expectedLocalIp = "192.168.1.100";
        var expectedMask = "255.255.255.0";
        var expectedGw = "192.168.1.1";

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("miIO.info"))))
            .Returns(ToJson(new
            {
                id = 1,
                result = new
                {
                    uptime = expectedUptime,
                    miio_ver = expectedMiioVersion,
                    mac = expectedMac,
                    fw_ver = expectedFirmwareVersion,
                    hw_ver = expectedHardwareVersion,
                    ap = new
                    {
                        ssid = expectedSsid,
                        bssid = expectedBssid,
                        rssi = expectedRssi,
                        freq = expectedFreq,
                    },
                    netif = new
                    {
                        localIp = expectedLocalIp,
                        mask = expectedMask,
                        gw = expectedGw,
                    }
                }
            }));


        // Assert        
        _gw.Object.UptimeSeconds.Should().Be(expectedUptime);
        _gw.Object.MiioVersion.Should().Be(expectedMiioVersion);
        _gw.Object.Mac.Should().Be(expectedMac);
        _gw.Object.FirmwareVersion.Should().Be(expectedFirmwareVersion);
        _gw.Object.Hardware.Should().Be(expectedHardwareVersion);
        _gw.Object.Wifi.Ssid.Should().Be(expectedSsid);
        _gw.Object.Wifi.Bssid.Should().Be(expectedBssid);
        _gw.Object.Wifi.Rssi.Should().Be(expectedRssi);
        _gw.Object.Wifi.Freq.Should().Be(expectedFreq);
        _gw.Object.Wifi.Freq.Should().Be(expectedFreq);
        _gw.Object.Network.Ip.Should().Be(expectedLocalIp);
        _gw.Object.Network.Mask.Should().Be(expectedMask);
        _gw.Object.Network.Gateway.Should().Be(expectedGw);
    }

    [Fact]
    public void Get_AccessMode_Returns_Correct_Network_Mode()
    {
        // Arrange
        var expected = MultimodeGateway2.AccessModeValue.Wireless2G;

        SetupGetProperties((2, 1, _gwDid, expected));

        // Act
        var accessMode = _gw.Object.AccessMode;

        // Assert
        VerifyGetProperties(2, 1, _gwDid);
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
        VerifySetProperties(2, 1, _gwDid, (int)expected, 2);
    }

    [Fact]
    public void Get_DoNotDisturbModeEnabled_Returns_Correct_DoNotDisturbMode_Mode()
    {
        // Arrange
        SetupGetProperties((6, 1, _gwDid, 0));

        // Act
        var enabled = _gw.Object.DoNotDisturbModeEnabled;

        // Assert
        VerifyGetProperties(6, 1, _gwDid);
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
        VerifySetProperties(6, 1, _gwDid, expected, 2);
    }

    [Fact]
    public void Get_DoNotDisturbEffectiveTime_Returns_Correct_DoNotDisturbEffectiveTime()
    {
        // Arrange
        var (expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute) = ((ushort)23, (ushort)05, (ushort)08, (ushort)10);
        var str = $"{expectedStartHour:D2}:{exectedStartMinute:D2}-{expectedEndHour:D2}:{expectedEndMinute:D2}";

        SetupGetProperties((6, 2, _gwDid, str));

        // Act
        var (startHour, startMinute, endHour, endMinute) = _gw.Object.DoNotDisturbEffectiveTime;

        // Assert
        VerifyGetProperties(6, 2, _gwDid);
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
        VerifySetProperties(6, 2, _gwDid, expected, 2);
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

        SetupGetProperties((6, 3, _gwDid, expected));

        // Act
        var ledBrightness = _gw.Object.LedIndicatorBrightness;

        // Assert
        VerifyGetProperties(6, 3, _gwDid);
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
        VerifySetProperties(6, 3, _gwDid, expected, 2);
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
        SetupGetProperties((7, 1, _gwDid, 1));

        // Act
        var accidentalDeletion = _gw.Object.AccidentalDeletionEnabled;

        // Assert
        VerifyGetProperties(7, 1, _gwDid);
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
        VerifySetProperties(7, 1, _gwDid, expected, 2);
    }
}
