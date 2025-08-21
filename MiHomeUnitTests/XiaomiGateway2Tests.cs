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

        _gateway = new XiaomiGateway2(_miioTransport.Object, _messageTransport.Object, _gatewaySid);
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
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new() 
            { 
                { "result", new[] {"f40e1b285fes68cd"} },
                { "id", 1 } 
            })));
        
        // Act
        var key = await _gateway.GetDeveloperKeyAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_lumi_dpf_aes_key" },
                {"params", Array.Empty<string>()},
            })), Times.Once());

        key.Should().Be("f40e1b285fes68cd");
    }

    [Fact]
    public void EnableLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 1677786880;
        var brightness = 100;
        
        SendResultMethodAsync("set_rgb", "ok");

        // Act
        _ = _gateway.EnableLightAsync(0, 255, 0, brightness);

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
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToJson(new()
            {
                { "result", new[] { "on"} },
                { "id", 1 },                
            }));

        // Act
        var arming = _gateway.IsArmingOn();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new(){
                { "id", 1 },
                { "method", "get_arming" },
                { "params", Array.Empty<string>() },
            })), Times.Once());

        arming.Should().BeTrue();
    }

    [Fact]
    public async Task IsArmingOnAsync_Returns_Arming_StateAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new()
            {
                { "result", new[] { "off"} },
                { "id", 1 },  
            })));
        
        // Act
        var arming = await _gateway.IsArmingOnAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new(){
                { "id", 1 },
                { "method", "get_arming" },
                { "params", Array.Empty<string>() },
            })), Times.Once());

        arming.Should().BeFalse();
    }

    [Fact]
    public void SetArmingOn_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());

        // Act
        _gateway.SetArmingOn();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                {"id", 1},
                {"method", "set_arming"},
                {"params", new[]{ "on" }},
            })), Times.Once());
    }

    [Fact]
    public async Task SetArmingOnAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ResultOkJson()));
        
        // Act
        await _gateway.SetArmingOnAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                {"id", 1},
                {"method", "set_arming"},
                {"params", new[] { "on" }},
            })), Times.Once());
    }

    [Fact]
    public void SetArmingOff_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());
        
        // Act
        _gateway.SetArmingOff();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                {"id", 1},
                {"method", "set_arming"},
                {"params", new[] { "off" }},
            })), Times.Once());
    }

    [Fact]
    public async Task SetArmingOffAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ResultOkJson()));
        
        // Act
        await _gateway.SetArmingOffAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                {"id", 1},
                {"method", "set_arming"},
                {"params", new[] { "off" }},
            })), Times.Once());
    }

    [Fact]
    public void GetArmingWaitTime_Returns_Integer()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToJson(new() 
            { 
                { "result", new[] {15} },
                { "id", 1 } 
            }));
        
        // Act
        var armingWaitTime = _gateway.GetArmingWaitTime();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_arm_wait_time" },
                {"params", Array.Empty<string>()},
            })), Times.Once());

        armingWaitTime.Should().Be(15);
    }

    [Fact]
    public async Task GetArmingWaitTimeAsync_Returns_IntegerAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new() 
            { 
                { "result", new[] {30} },
                { "id", 1 } 
            })));
        
        // Act
        var armingWaitTime = await _gateway.GetArmingWaitTimeAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_arm_wait_time" },
                {"params", Array.Empty<string>()},
            })), Times.Once());

        armingWaitTime.Should().Be(30);
    }

    [Fact]
    public void SetArmingWaitTime_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());
        
        // Act
        _gateway.SetArmingWaitTime(20);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "set_arm_wait_time" },
                {"params", new[] {20}},
            })), Times.Once());
    }

    [Fact]
    public async Task SetWaitTimeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ResultOkJson()));
        
        // Act
        await _gateway.SetArmingWaitTimeAsync(30);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "set_arm_wait_time" },
                {"params", new[] {30}},
            })), Times.Once());
    }

    [Fact]
    public void GetArmingOffTime_Returns_Integer()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToJson(new() 
            { 
                { "result", new[] {15} },
                { "id", 1 } 
            }));
        
        // Act
        var armingOffTime = _gateway.GetArmingOffTime();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "get_device_prop" },
                { "params", new[] { "lumi.0", "alarm_time_len"} },
            })), Times.Once());

        armingOffTime.Should().Be(15);
    }

    [Fact]
    public async Task GetArmingOffTimeAsync_Returns_IntegerAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new() 
            { 
                { "result", new[] {30} },
                { "id", 1 } 
            })));
        
        // Act
        var armingOffTime = await _gateway.GetArmingOffTimeAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "get_device_prop" },
                { "params", new[] { "lumi.0", "alarm_time_len"} },
            })), Times.Once());
        
        armingOffTime.Should().Be(30);
    }

    [Fact]
    public void SetArmingOffTime_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());
        
        // Act
        _gateway.SetArmingOffTime(40);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "set_device_prop" },
                { "params", new { sid = "lumi.0", alarm_time_len = 40 } },
            })), Times.Once());
    }

    [Fact]
    public async Task SetArmingOffTimeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ResultOkJson()));
        
        // Act
        await _gateway.SetArmingOffTimeAsync(45);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "set_device_prop" },
                { "params", new { sid = "lumi.0", alarm_time_len = 45 } },
            })), Times.Once());
    }

    [Fact]
    public void GetArmingBlinkingTime_Returns_Integer()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToJson(new() 
            { 
                { "result", new[] {15} },
                { "id", 1 } 
            }));
        
        // Act
        var armingBlinkingTime = _gateway.GetArmingBlinkingTime();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "get_device_prop" },
                { "params", new[] { "lumi.0", "en_alarm_light"} },
            })), Times.Once());
        
        armingBlinkingTime.Should().Be(15);
    }

    [Fact]
    public async Task GetArmingBlinkingTimeAsync_Returns_IntegerAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new() 
            { 
                { "result", new[] {30} },
                { "id", 1 } 
            })));
        
        // Act
        var armingBlinkingTime = await _gateway.GetArmingBlinkingTimeAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "get_device_prop" },
                { "params", new[] { "lumi.0", "en_alarm_light"} },
            })), Times.Once());
                
        armingBlinkingTime.Should().Be(30);
    }

    [Fact]
    public void SetArmingBlinkingTime_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());
        
        // Act
        _gateway.SetArmingBlinkingTime(40);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "set_device_prop" },
                { "params", new { sid = "lumi.0", en_alarm_light = 40 } },
            })), Times.Once());
    }

    [Fact]
    public async Task SetArmingBlinkingTimeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ResultOkJson()));
        
        // Act
        await _gateway.SetArmingBlinkingTimeAsync(45);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 }, 
                { "method", "set_device_prop" },
                { "params", new { sid = "lumi.0", en_alarm_light = 45 }}
            })), Times.Once());
    }

    [Fact]
    public void GetArmingVolume_Returns_Integer()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToJson(new()
            {
                { "result", new[] {10} },
                { "id", 1 },
            }));
        
        // Act
        var armingVolume = _gateway.GetArmingVolume();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_alarming_volume" },
                { "params", Array.Empty<int>() },
            })), Times.Once());
        
        armingVolume.Should().Be(10);
    }

    [Fact]
    public async Task GetArmingVolumeAsync_Returns_IntegerAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new()
            {
                { "result", new[] {20} },
                { "id", 1 },
            })));
        
        // Act
        var armingVolume = await _gateway.GetArmingVolumeAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_alarming_volume" },
                { "params", Array.Empty<int>() },
            })), Times.Once());
        
        armingVolume.Should().Be(20);
    }

    [Fact]
    public void SetArmingVolume_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());
        
        // Act
        _gateway.SetArmingVolume(15);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "set_alarming_volume" },
                { "params", new[] {15} },
            })), Times.Once());
    }

    [Fact]
    public async Task SetArmingVolumeAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ResultOkJson()));
        
        // Act
        await _gateway.SetArmingVolumeAsync(30);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "set_alarming_volume" },
                { "params", new[] {30} },
            })), Times.Once());
    }

    [Fact]
    public void GetArmingLastTimeTriggeredTimestamp_Returns_Integer()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ToJson(new()
            {
                { "result", new[] {1609150074} },
                { "id", 1 },
            }));
        
        // Act
        var timestamp = _gateway.GetArmingLastTimeTriggeredTimestamp();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_arming_time" },
                { "params", Array.Empty<int>() },
            })), Times.Once());
        
        timestamp.Should().Be(1609150074);
    }

    [Fact]
    public async Task GetArmingLastTimeTriggeredTimestampAsync_Returns_IntegerAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(ToJson(new()
            {
                { "result", new[] {1609150074} },
                { "id", 1 },
            })));
        
        // Act
        var timestamp = await _gateway.GetArmingLastTimeTriggeredTimestampAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_arming_time" },
                { "params", Array.Empty<int>() },
            })), Times.Once());
        
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
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_channels" },
                { "params", new { start = 0} },
            })), Times.Once());

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
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "get_channels" },
                { "params", new { start = 0} },
            })), Times.Once());

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

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("add_channels"))))
            .Returns(ResultOkJson(2));

        // Act
        _gateway.AddRadioChannel(1045, "http://192.168.1.1/radio4.m3u8");

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 2 },
                { "method", "add_channels" },
                { "params", new { chs = new[] {new { id = 1045, url = "http://192.168.1.1/radio4.m3u8", type = 0}} } },
            })), Times.Once());
    }

    [Fact]
    public async Task AddRadioChannelAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));
        
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("add_channels"))))
            .Returns(Task.FromResult(ResultOkJson(2)));

        // Act
        await _gateway.AddRadioChannelAsync(1045, "http://192.168.1.1/radio4.m3u8");

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 2 },
                { "method", "add_channels" },
                { "params", new { chs = new[] {new { id = 1045, url = "http://192.168.1.1/radio4.m3u8", type = 0}} } },
            })), Times.Once());
    }

    [Fact]
    public void RemoveRadioChannel_Should_Throw_Exception_When_Non_Existing_Id()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        // Act & Assert
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

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("remove_channels"))))
            .Returns(ResultOkJson(2));

        // Act
        _gateway.RemoveRadioChannel(1027);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 2 },
                { "method", "remove_channels" },
                { "params", new { chs = new[] {new { id = 1027, url = "http://192.168.1.1/radio1027.m3u8", type = 0}} }  },
            })), Times.Once());
    }

    [Fact]
    public async Task RemoveRadioChannelAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange        
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("remove_channels"))))
            .Returns(Task.FromResult(ResultOkJson(2)));

        // Act
        await _gateway.RemoveRadioChannelAsync(1025);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 2 },
                { "method", "remove_channels" },
                { "params", new { chs = new[] {new { id = 1025, url = "http://192.168.1.1/radio1025.m3u8", type = 0}} }  },
            })), Times.Once());
    }

    [Fact]
    public void RemoveAllRadioChannels_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(ToRadioListJson([1025, 1026, 1027]));

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("remove_channels"))))
            .Returns(ResultOkJson(2));

        // Act
        _gateway.RemoveAllRadioChannels();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 2 },
                { "method", "remove_channels" },
                { "params", new { chs = new[] 
                    {
                        new { id = 1025, url = "http://192.168.1.1/radio1025.m3u8", type = 0},
                        new { id = 1026, url = "http://192.168.1.1/radio1026.m3u8", type = 0},
                        new { id = 1027, url = "http://192.168.1.1/radio1027.m3u8", type = 0},
                    } }  },
            })), Times.Once());
    }

    [Fact]
    public async Task RemoveAllRadioChannelsAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("remove_channels"))))
            .Returns(Task.FromResult(ResultOkJson(2)));

        // Act
        await _gateway.RemoveAllRadioChannelsAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 2 },
                { "method", "remove_channels" },
                { "params", new { chs = new[] 
                    {
                        new { id = 1025, url = "http://192.168.1.1/radio1025.m3u8", type = 0},
                        new { id = 1026, url = "http://192.168.1.1/radio1026.m3u8", type = 0},
                        new { id = 1027, url = "http://192.168.1.1/radio1027.m3u8", type = 0},
                    } }  },
            })), Times.Once());
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

        // Act & Assert
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

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("play_specify_fm"))))
            .Returns(ResultOkJson(2));

        // Act
        _gateway.PlayRadio(1027, 50);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 2 },
                { "method", "play_specify_fm" },
                { "params", new[] {1027,50} },
            })), Times.Once());
    }

    [Fact]
    public async Task PlayRadioAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson([1025, 1026, 1027])));

        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("play_specify_fm"))))
            .Returns(Task.FromResult(ResultOkJson(2)));

        // Act
        await _gateway.PlayRadioAsync(1025, 50);

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 2 },
                { "method", "play_specify_fm" },
                { "params", new[] {1025, 50} },
            })), Times.Once());
    }

    [Fact]
    public void StopRadio_Should_Not_Throw_Exceptions()
    {
        // Arrange        
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("play_fm"))))
            .Returns(ResultOkJson());

        // Act
        _gateway.StopRadio();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", "play_fm" },
                { "params", new[] {"off"} },
            })), Times.Once());
    }

    [Fact]
    public async Task StopRadioAsync_Should_Not_Throw_ExceptionsAsync()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("play_fm"))))
            .Returns(Task.FromResult(ResultOkJson()));

        // Act
        await _gateway.StopRadioAsync();

        // Assert
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", "play_fm" },
                { "params", new[] {"off"} },
            })), Times.Once());
    }
}
