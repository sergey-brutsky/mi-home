using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using MiHomeLib.XiaomiGateway2;
using MiHomeLib.XiaomiGateway2.Devices;
using MiHomeLib.XiaomiGateway2.Commands;
using System.Threading.Tasks;
using MiHomeLib.Transport;
using System;
using System.Text.Json;
using System.Linq;
using MiHomeUnitTests.XiaomiGateway2SubDevicesTests;
using MiHomeLib;

namespace MiHomeUnitTests;

public class XiaomiGateway2Tests: Gw2DeviceTests
{
    private readonly string _gatewaySid;
    private readonly XiaomiGateway2 _gateway;
    
    private static string ToRadioListJson(int[] radioChannelIds)
    {
        var list = radioChannelIds
            .Select(x => new { id = x, type = 0, url = $"http://192.168.1.1/radio{x}.m3u8"});

        return JsonSerializer.Serialize(new { result = new { chs = list }});
    }
    
    public XiaomiGateway2Tests()
    {
        _gatewaySid = _fixture.Create<string>()[..12];

        _miioTransport = new Mock<IMiioTransport>();
        SendResultMethodAsync(string.Empty, ["f40e1b285fes68cd"]);

        _gateway = new XiaomiGateway2(_miioTransport.Object, _messageTransport.Object, _gatewaySid, -1);
    }

    [Fact]
    public void OnDeviceDiscovered_DiscoversGateway()
    {        
        var cmd1 = new Gw2Response()
        {
            Cmd = "get_id_list_ack",
            Sid = _gatewaySid,
            Token = _fixture.Create<string>(),
            Data = $"[\"{_gatewaySid}\"]"
        };

        var rgb = (int)_fixture.Create<byte>();
        var illumination = (int)_fixture.Create<byte>();
        var protoVersion = "1.1.1";

        var cmd2 = new Gw2Response()
        {
            Cmd = "read_ack",
            Sid = _gatewaySid,
            Model = "gateway",
            Data = $"{{\"rgb\":{rgb},\"illumination\":{illumination},\"proto_version\":\"{protoVersion}\"}}"
        };

        var eventRaised = false;
        XiaomiMultifunctionalGateway2 actualGw = null;

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            if(device is XiaomiMultifunctionalGateway2 gw) 
            {
                eventRaised  = true;
                actualGw = gw;
            }
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendCommand(It.IsAny<DiscoverGatewayCommand>())).Returns(0)
            .Raises(x => x.OnMessageReceived += null, cmd1.ToString());

        _messageTransport
            .Setup(x => x.SendCommand(It.IsAny<ReadDeviceCommand>())).Returns(0)
            .Raises(x => x.OnMessageReceived += null, cmd2.ToString());

        _gateway.DiscoverDevices();

        eventRaised.Should().BeTrue();
        actualGw.Should().NotBeNull();
        actualGw.Rgb.Should().Be(rgb);
        actualGw.Illumination.Should().Be(illumination);
        actualGw.ProtoVersion.Should().Be(protoVersion);
    }

    [Fact]
    public void OnDeviceDiscovered_DiscoversChildrenSubDevices()
    {        
        var waterLeakSid = _fixture.Create<string>();
        var thSensorSid =  _fixture.Create<string>();

        var cmd1 = new Gw2Response()
        {
            Cmd = "get_id_list_ack",
            Sid = _gatewaySid,
            Token = _fixture.Create<string>(),
            Data = $"[\"{waterLeakSid}\",\"{thSensorSid}\"]"
        };

        var cmd2 = new Gw2Response()
        {
            Cmd = "read_ack",
            Sid = waterLeakSid,
            Model = "sensor_wleak.aq1",
            Data = $"{{\"voltage\":3015}}"
        };

        var cmd3 = new Gw2Response()
        {
            Cmd = "read_ack",
            Sid = thSensorSid,
            Model = "sensor_ht",
            Data = $"{{\"voltage\":3025,\"temperature\":\"2363\",\"humidity\":\"2902\"}}"
        };

        var waterLeakSensorDiscovered = false;
        var thSensorDiscovered = false;
        
        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            if(device is AqaraWaterLeakSensor) waterLeakSensorDiscovered = true;
            if(device is XiaomiTemperatureHumiditySensor) thSensorDiscovered = true;
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendCommand(It.IsAny<DiscoverGatewayCommand>())).Returns(0)
            .Raises(x => x.OnMessageReceived += null, cmd1.ToString());

        _messageTransport
            .Setup(x => x.SendCommand(It.Is<ReadDeviceCommand>(cmd => cmd.Sid == waterLeakSid))).Returns(0)
            .Raises(x => x.OnMessageReceived += null, cmd2.ToString());

        _messageTransport
            .Setup(x => x.SendCommand(It.Is<ReadDeviceCommand>(cmd => cmd.Sid == thSensorSid))).Returns(0)
            .Raises(x => x.OnMessageReceived += null, cmd3.ToString());

        _gateway.DiscoverDevices();

        waterLeakSensorDiscovered.Should().BeTrue();
        thSensorDiscovered.Should().BeTrue();
    }

    [Fact]
    public void SetDeveloperKeyOfWrongLength_Should_Throw_Exceptions()
    {
        // Arrange
        var key = _fixture.Create<string>()[..3]; // key of length 3 charactes        

        SendResultMethodAsync("set_lumi_dpf_aes_key", "ok");

        // Act  
        var actual = _gateway.Invoking(x => x.SetDeveloperKey(key));

        // Assert
        actual
            .Should()
            .Throw<ArgumentException>()
            .WithMessage("Developer key must be exactly 16 characters long");
    }

    [Fact]
    public void SetDeveloperKey_Should_SendMessage_Successfully()
    {
        // Arrange
        var key = _fixture.Create<string>()[..16]; // exactly 16 characters
        
        SendResultMethodAsync("set_lumi_dpf_aes_key", "ok");

        // Act
        _gateway.SetDeveloperKey(key);

        // Assert
        VerifyMethodAsync("set_lumi_dpf_aes_key", key);
    }

    [Fact]
    public async Task GetDeveloperKey_Returns_CorrectKey()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["f40e1b285fes68cd"]);
        
        // Act
        var key = await _gateway.GetDeveloperKeyAsync();

        // Assert
        VerifyMethodAsync("get_lumi_dpf_aes_key", Array.Empty<string>());
        key.Should().Be("f40e1b285fes68cd");
    }

    [Fact]
    public async Task EnableLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 1677786880;
        var brightness = 100;
        
        SendResultMethodAsync("set_rgb", "ok");

        // Act
        await _gateway.EnableLightAsync(0, 255, 0, brightness);

        // Assert
        VerifyMethodAsync("set_rgb", rgb, brightness);
    }

    [Fact]
    public void DisableLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 0;
        var brightness = 0;
        
        SendResultMethodAsync("set_rgb", "ok");

        // Act
        _ = _gateway.DisableLightAsync();

        // Assert
        VerifyMethodAsync("set_rgb", rgb, brightness);
    }

    [Fact]
    public void EnableNightLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 1677786880;
        var brightness = 100;
        
        SendResultMethodAsync("set_night_light_rgb", "ok");

        // Act
        _ = _gateway.EnableNightLightAsync(0, 255, 0, brightness);

        // Assert
        VerifyMethodAsync("set_night_light_rgb", rgb, brightness);
    }

    [Fact]
    public void DisableNightLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 0;
        var brightness = 0;
        
        SendResultMethodAsync("set_night_light_rgb", "ok");

        // Act
        _ = _gateway.DisableNightLightAsync();

        // Assert
        VerifyMethodAsync("set_night_light_rgb", rgb, brightness);
    }

    [Fact]
    public void PlaySound_Should_PlaySound_Successfully()
    {
        // Arrange
        var sound = XiaomiGateway2.Sound.Mimix;
        var volume = 3;
        
        SendResultMethodAsync("play_music_new", "ok");

        // Act
        _ = _gateway.PlaySoundAsync(sound, volume);

        // Assert
        VerifyMethodAsync("play_music_new", ((int)sound).ToString(), volume);
    }

    [Fact]
    public void SoundsOff_Should_Stop_Playing_Sound()
    {
        // Arrange
        SendResultMethodAsync("play_music_new", "ok");

        // Act
        _gateway.SoundsOff();

        // Assert
        VerifyMethodAsync("play_music_new", "0", 0);
    }

    [Fact]
    public void PlayCustomSound_Should_Play_CustomSound_Successfully()
    {
        // Arrange
        var sound = 10_001;
        var volume = 3;
        
        SendResultMethodAsync("play_music_new", "ok");

        // Act
        _ = _gateway.PlayCustomSoundAsync(sound, volume);

        // Assert
        VerifyMethodAsync("play_music_new", sound.ToString(), volume);
    }

    [Fact]
    public void IsArmingOn_Returns_Arming_State()
    {
        // Arrange
        SendResultMethod(string.Empty, ["on"]);
        
        // Act
        var arming = _gateway.IsArmingOn();

        // Assert
        VerifyMethod("get_arming", []);        
        arming.Should().BeTrue();
    }

    [Fact]
    public async Task IsArmingOnAsync_Returns_Arming_StateAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["off"]);
        
        // Act
        var arming = await _gateway.IsArmingOnAsync();

        // Assert
        VerifyMethodAsync("get_arming", Array.Empty<string>());
        arming.Should().BeFalse();
    }

    [Fact]
    public void SetArmingOn_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod(string.Empty, ["ok"]);

        // Act
        _gateway.SetArmingOn();

        // Assert
        VerifyMethod("set_arming", ["on"]);
    }

    [Fact]
    public async Task SetArmingOnAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["ok"]);
        
        // Act
        await _gateway.SetArmingOnAsync();

        // Assert
        VerifyMethodAsync("set_arming", ["on"]);
    }

    [Fact]
    public void SetArmingOff_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod(string.Empty, ["ok"]);
        
        // Act
        _gateway.SetArmingOff();

        // Assert
        VerifyMethod("set_arming", ["off"]);
    }

    [Fact]
    public async Task SetArmingOffAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["ok"]);
        
        // Act
        await _gateway.SetArmingOffAsync();

        // Assert
        VerifyMethodAsync("set_arming", ["off"]);
    }

    [Fact]
    public void GetArmingWaitTime_Returns_Integer()
    {
        // Arrange
        SendResultMethod(string.Empty, [15]);
        
        // Act
        var armingWaitTime = _gateway.GetArmingWaitTime();

        // Assert
        VerifyMethod("get_arm_wait_time",[]);

        armingWaitTime.Should().Be(15);
    }

    [Fact]
    public async Task GetArmingWaitTimeAsync_Returns_IntegerAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, [30]);
        
        // Act
        var armingWaitTime = await _gateway.GetArmingWaitTimeAsync();

        // Assert
        VerifyMethodAsync("get_arm_wait_time", []);
        armingWaitTime.Should().Be(30);
    }

    [Fact]
    public void SetArmingWaitTime_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod(string.Empty, ["ok"]);
        
        // Act
        _gateway.SetArmingWaitTime(20);

        // Assert
        VerifyMethod("set_arm_wait_time", [20]);
    }

    [Fact]
    public async Task SetWaitTimeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["ok"]);
        
        // Act
        await _gateway.SetArmingWaitTimeAsync(30);

        // Assert
        VerifyMethodAsync("set_arm_wait_time", [30]);
    }

    [Fact]
    public void GetArmingOffTime_Returns_Integer()
    {
        // Arrange
        SendResultMethod(string.Empty, [15]);
        
        // Act
        var armingOffTime = _gateway.GetArmingOffTime();

        // Assert
        VerifyMethod("get_device_prop", ["lumi.0", "alarm_time_len"]);
        armingOffTime.Should().Be(15);
    }

    [Fact]
    public async Task GetArmingOffTimeAsync_Returns_IntegerAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, [30]);
        
        // Act
        var armingOffTime = await _gateway.GetArmingOffTimeAsync();

        // Assert
        VerifyMethodAsync("get_device_prop", ["lumi.0", "alarm_time_len"]);        
        armingOffTime.Should().Be(30);
    }

    [Fact]
    public void SetArmingOffTime_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod(string.Empty, ["ok"]);
        
        // Act
        _gateway.SetArmingOffTime(40);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 1,
                method = "set_device_prop",
                @params = new { sid = "lumi.0", alarm_time_len = 40 },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public async Task SetArmingOffTimeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["ok"]);
        
        // Act
        await _gateway.SetArmingOffTimeAsync(45);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 1,
                method = "set_device_prop",
                @params = new { sid = "lumi.0", alarm_time_len = 45 },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public void GetArmingBlinkingTime_Returns_Integer()
    {
        // Arrange
        SendResultMethod(string.Empty, [15]);
        
        // Act
        var armingBlinkingTime = _gateway.GetArmingBlinkingTime();

        // Assert
        VerifyMethod("get_device_prop", ["lumi.0", "en_alarm_light"]);        
        armingBlinkingTime.Should().Be(15);
    }

    [Fact]
    public async Task GetArmingBlinkingTimeAsync_Returns_IntegerAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, [30]);
        
        // Act
        var armingBlinkingTime = await _gateway.GetArmingBlinkingTimeAsync();

        // Assert
        VerifyMethodAsync("get_device_prop", ["lumi.0", "en_alarm_light"]);                
        armingBlinkingTime.Should().Be(30);
    }

    [Fact]
    public void SetArmingBlinkingTime_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod(string.Empty, ["ok"]);
        
        // Act
        _gateway.SetArmingBlinkingTime(40);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 1,
                method = "set_device_prop",
                @params = new { sid = "lumi.0", en_alarm_light = 40 },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public async Task SetArmingBlinkingTimeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["ok"]);
        
        // Act
        await _gateway.SetArmingBlinkingTimeAsync(45);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 1,
                method = "set_device_prop",
                @params = new { sid = "lumi.0", en_alarm_light = 45 }
            }.ToJson()), Times.Once());
    }

    [Fact]
    public void GetArmingVolume_Returns_Integer()
    {
        // Arrange
        SendResultMethod(string.Empty, [10]);
        
        // Act
        var armingVolume = _gateway.GetArmingVolume();

        // Assert
        VerifyMethod("get_alarming_volume", []);        
        armingVolume.Should().Be(10);
    }

    [Fact]
    public async Task GetArmingVolumeAsync_Returns_IntegerAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, [20]);
        
        // Act
        var armingVolume = await _gateway.GetArmingVolumeAsync();

        // Assert
        VerifyMethodAsync("get_alarming_volume", []);
        armingVolume.Should().Be(20);
    }

    [Fact]
    public void SetArmingVolume_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod(string.Empty, ["ok"]);
        
        // Act
        _gateway.SetArmingVolume(15);

        // Assert
        VerifyMethod("set_alarming_volume", [15]);
    }

    [Fact]
    public async Task SetArmingVolumeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, ["ok"]);
        
        // Act
        await _gateway.SetArmingVolumeAsync(30);

        // Assert
        VerifyMethodAsync("set_alarming_volume", [30]);
    }

    [Fact]
    public void GetArmingLastTimeTriggeredTimestamp_Returns_Integer()
    {
        // Arrange
        SendResultMethod(string.Empty, [1609150074]);        
        
        // Act
        var timestamp = _gateway.GetArmingLastTimeTriggeredTimestamp();

        // Assert
        VerifyMethod("get_arming_time", []);
        timestamp.Should().Be(1609150074);
    }

    [Fact]
    public async Task GetArmingLastTimeTriggeredTimestampAsync_Returns_IntegerAsync()
    {
        // Arrange
        SendResultMethodAsync(string.Empty, [1609150074]);
        
        // Act
        var timestamp = await _gateway.GetArmingLastTimeTriggeredTimestampAsync();

        // Assert
        VerifyMethodAsync("get_arming_time", []);
        timestamp.Should().Be(1609150074);
    }

    [Fact]
    public void GetRadioChannels_Returns_List_of_RadioChannels()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        // Act
        var radioChannels = _gateway.GetRadioChannels();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 1,
                method = "get_channels",
                @params = new { start = 0 },
            }.ToJson()), Times.Once());

        radioChannels.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRadioChannels_Returns_List_of_RadioChannelAsync()
    {
        // Arrange        
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        // Act
        var radioChannels = await _gateway.GetRadioChannelsAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 1,
                method = "get_channels",
                @params = new { start = 0 },
            }.ToJson()), Times.Once());

        radioChannels.Should().HaveCount(3);
    }

    [Fact]
    public void AddRadioChannel_with_Id_less_than_1024_throws_exception()
    {
        // Act
        var actual = _gateway.Invoking(x => x.AddRadioChannel(1000, "url here"));

        // Assert
        actual.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRadioChannel_with_existing_Id_throws_exception()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToRadioListJson([1025, 1045, 1027]));

        // Act
        var actual = _gateway.Invoking(x => x.AddRadioChannel(1045, "http://192.168.1.1/radio.m3u8"));
        // Assert
        actual.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRadioChannel_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        SendResultMethod("add_channels", ["ok"]);
        // _miioTransport
        //     .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("add_channels"))))
        //     .Returns(ResultOkJson(2));

        // Act
        _gateway.AddRadioChannel(1045, "http://192.168.1.1/radio4.m3u8");

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 2,
                method = "add_channels",
                @params = new { chs = new[] {new { id = 1045, url = "http://192.168.1.1/radio4.m3u8", type = 0}} },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public async Task AddRadioChannelAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));
        
        SendResultMethodAsync("add_channels", ["ok"]);

        // Act
        await _gateway.AddRadioChannelAsync(1045, "http://192.168.1.1/radio4.m3u8");

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 2,
                method = "add_channels",
                @params = new { chs = new[] {new { id = 1045, url = "http://192.168.1.1/radio4.m3u8", type = 0}} },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public void RemoveRadioChannel_Should_Throw_Exception_When_Non_Existing_Id()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        // Act
        var actual = _gateway.Invoking(x => x.RemoveRadioChannel(1045));

        // Assert
        actual.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveRadioChannel_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        SendResultMethod("remove_channels", ["ok"]);

        // Act
        _gateway.RemoveRadioChannel(1027);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 2,
                method = "remove_channels",
                @params = new { chs = new[] {new { id = 1027, url = "http://192.168.1.1/radio1027.m3u8", type = 0}} },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public async Task RemoveRadioChannelAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange        
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        SendResultMethodAsync("remove_channels", ["ok"]);

        // Act
        await _gateway.RemoveRadioChannelAsync(1025);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 2,
                method = "remove_channels",
                @params = new { chs = new[] {new { id = 1025, url = "http://192.168.1.1/radio1025.m3u8", type = 0}} },
            }.ToJson()), Times.Once());
    }

    [Fact]
    public void RemoveAllRadioChannels_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        SendResultMethod("remove_channels", ["ok"]);

        // Act
        _gateway.RemoveAllRadioChannels();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 2,
                method = "remove_channels",
                @params = new { chs = new[] 
                {
                    new { id = 1025, url = "http://192.168.1.1/radio1025.m3u8", type = 0 },
                    new { id = 1026, url = "http://192.168.1.1/radio1026.m3u8", type = 0 },
                    new { id = 1027, url = "http://192.168.1.1/radio1027.m3u8", type = 0 },
                }},
            }.ToJson()), Times.Once());
    }

    [Fact]
    public async Task RemoveAllRadioChannelsAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        SendResultMethodAsync("remove_channels", ["ok"]);

        // Act
        await _gateway.RemoveAllRadioChannelsAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 2,
                method = "remove_channels",
                @params = new { chs = new[] 
                {
                    new { id = 1025, url = "http://192.168.1.1/radio1025.m3u8", type = 0 },
                    new { id = 1026, url = "http://192.168.1.1/radio1026.m3u8", type = 0 },
                    new { id = 1027, url = "http://192.168.1.1/radio1027.m3u8", type = 0 },
                }},
            }.ToJson()), Times.Once());
    }

    [Fact]
    public void PlayRadio_Should_Throw_Exception_When_Wrong_Volume()
    {
        // Act & Assert
        var actual = _gateway.Invoking(x => x.PlayRadio(1045, 120));
        // Assert
        actual.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PlayRadio_Should_Throw_Exception_When_Wrong_ChannelId()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        // Act
        var actual = _gateway.Invoking(x => x.PlayRadio(1045, 50));

        // Assert
        actual.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PlayRadio_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        SendResultMethod("play_specify_fm", ["ok"]);

        // Act
        _gateway.PlayRadio(1027, 50);

        // Assert
         _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = 2,
                method = "play_specify_fm",
                @params = new[] {1027, 50},
            }.ToJson()), Times.Once());
    }

    [Fact]
    public async Task PlayRadioAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        SendResultMethodAsync("play_specify_fm", ["ok"]);
        
        // Act
        await _gateway.PlayRadioAsync(1025, 50);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = 2,
                method = "play_specify_fm",
                @params = new[] {1025, 50},
            }.ToJson()), Times.Once());
    }

    [Fact]
    public void StopRadio_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("play_fm", ["ok"]);

        // Act
        _gateway.StopRadio();

        // Assert
        VerifyMethod("play_fm", ["off"]);
    }

    [Fact]
    public async Task StopRadioAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("play_fm", ["ok"]);

        // Act
        await _gateway.StopRadioAsync();

        // Assert
        VerifyMethodAsync("play_fm", ["off"]);
    }
}
