using MiHomeUnitTests.Devices.Mqtt;
using Xunit;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using System;
using AutoFixture;
using Moq;
using System.Threading.Tasks;

namespace MiHomeUnitTests.Gateways.Mqtt;

public class AqaraHubE1ChinaTests : MqttDeviceTestBase
{
    private const string GW_DID = "1111111111";
    private readonly AqaraHubE1China _gw;

    public AqaraHubE1ChinaTests()
    {
        _gw = new AqaraHubE1China(GW_DID, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object, _loggerFactory);
    }

    [Fact]
    public void Dispose_Unsubscribes_And_Disposes_Both_Transports()
    {
        _devicesDiscoverer.Setup(x => x.DiscoverZigBeeDevices()).Returns([]);
        _gw.DiscoverDevices();
        _gw.Dispose();
        _mqttTransport.VerifyRemove(x => x.OnMessageReceived -= It.IsAny<Action<string, string>>(), Times.Once);
        _mqttTransport.Verify(x => x.Dispose(), Times.Once);
        _miioTransport.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void Get_LedEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((6, 1, GW_DID, 1));

        // Act
        var ledEnabled = _gw.GetNightModeEnabled();

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
        _gw.SetNightModeEnabled(false);

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
        var (startHour, startMinute, endHour, endMinute) = _gw.GetNightModeEffectiveHours();

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
        _gw.SetNightModeEffectiveHours((expectedStartHour, exectedStartMinute, expectedEndHour, expectedEndMinute));

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
            .Invoking(x => x.SetNightModeEffectiveHours((startHour, startMinute, endHour, endMinute)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_HotspotModeEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((19, 1, GW_DID, true));

        // Act
        var hotspotEnabled = _gw.GetHotspotModeEnabled();

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
        _gw.SetHotspotModeEnabled(false);

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
        var hotspotName = _gw.GetHotspotName();

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
        _gw.SetHotspotName(expected);

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
            .Invoking(x => x.SetHotspotName(expected))
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
        var hotspotPassword = _gw.GetHotspotPassword();

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
        _gw.SetHotspotPassword(expected);

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
            .Invoking(x => x.SetHotspotPassword(expected))
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
        var hotspotClients = _gw.GetHotspotClients();

        // Assert
        VerifyGetProperties(19, 4, GW_DID);
        hotspotClients[0].name.Should().Be("client1");
        hotspotClients[1].name.Should().Be("client2");
    }

    [Theory]
    [InlineData("[{\"ip\":\"1\",\"mac\":\"m\",\"name\":\"Sergey's phone\"}]", "Sergey's phone")]
    [InlineData("[{'ip':'1','mac':'m','name':'Sergey\\'s phone'}]", "Sergey's phone")]
    [InlineData("[{'ip':'1','mac':'m','name':'Телефон \"дом\"'}]", "Телефон \"дом\"")]
    public void Client_Names_Preserve_Quotes_And_Unicode(string json, string expected)
    {
        SetupGetProperties((19, 4, GW_DID, json));
        _gw.GetHotspotClients().Should().ContainSingle().Which.name.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 32)]
    [InlineData(true, 0)]
    [InlineData(true, 50)]
    public void String_Boundaries_Are_Sent_Unmodified(bool password, int length)
    {
        var value = new string('я', length);
        SetupSetProperties();
        if (password) _gw.SetHotspotPassword(value);
        else _gw.SetHotspotName(value);
        VerifySetProperties(19, password ? 3 : 2, GW_DID, value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Null_String_Is_Rejected_Without_Request(bool password)
    {
        Action set = () => { if (password) _gw.SetHotspotPassword(null); else _gw.SetHotspotName(null); };
        set.Should().Throw<ArgumentNullException>();
        _miioTransport.Verify(x => x.SendMessageAsync(It.Is<string>(v => v.Contains("set_properties"))), Times.Never);
    }

    [Theory]
    [InlineData("{\"result\":[{\"did\":\"1111111111\",\"code\":-4003}]}")]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":[{\"did\":\"1111111111\",\"code\":0}]}")]
    [InlineData("{\"result\":[{\"did\":\"1111111111\",\"code\":\"bad\",\"value\":1}]}")]
    [InlineData("{\"result\":[{\"did\":\"1111111111\",\"code\":0,\"value\":null}]}")]
    [InlineData("{\"result\":[{\"did\":\"1111111111\",\"code\":0,\"value\":1},{\"did\":\"1111111111\",\"code\":0,\"value\":2}]}")]
    [InlineData("{\"error\":{\"code\":-1,\"message\":\"failed\"}}")]
    public void Invalid_Miot_Response_Gives_Operation_Error(string response)
    {
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(v => v.Contains("get_properties")))).ReturnsAsync(response);
        Action read = () => _ = _gw.GetHotspotName();
        read.Should().Throw<InvalidOperationException>().WithMessage("*19/2*");
    }

    [Fact]
    public async Task Discovery_Only_Reads_Zigbee()
    {
        _devicesDiscoverer.Setup(x => x.DiscoverZigBeeDevices()).Returns([]);
        await _gw.DiscoverDevicesAsync();
        _devicesDiscoverer.Verify(x => x.DiscoverZigBeeDevices(), Times.Once);
        _devicesDiscoverer.Verify(x => x.DiscoverBleDevices(), Times.Never);
        _devicesDiscoverer.Verify(x => x.DiscoverMeshDevices(), Times.Never);
    }
}
