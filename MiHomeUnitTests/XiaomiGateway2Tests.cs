using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using MiHomeLib.XiaomiGateway2;
using MiHomeLib.XiaomiGateway2.Devices;
using MiHomeLib.XiaomiGateway2.Commands;
using System.Threading.Tasks;
using MiHomeLib.Contracts;
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

        _gateway = new XiaomiGateway2(_miioTransport.Object, _messageTransport.Object, _gatewaySid, -1, _loggerFactory);
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
    public void DiscoverDevices_Called_Twice_Handles_Each_Message_Once()
    {
        var cmd = new Gw2Response()
        {
            Cmd = "get_id_list_ack",
            Sid = _gatewaySid,
            Token = _fixture.Create<string>(),
            Data = "[]"
        };

        _messageTransport
            .Setup(x => x.SendCommand(It.IsAny<DiscoverGatewayCommand>())).Returns(0)
            .Raises(x => x.OnMessageReceived += null, cmd.ToString());

        _gateway.DiscoverDevices();
        _gateway.DiscoverDevices();

        _messageTransport.Verify(x => x.SendCommand(It.Is<ReadDeviceCommand>(c => c.Sid == _gatewaySid)), Times.Exactly(2));
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
    public async Task DiscoverDevicesAsync_Discovers_Children_SubDevices()
    {
        var waterLeakSid = _fixture.Create<string>();
        var thSensorSid = _fixture.Create<string>();
        var discovered = new System.Collections.Concurrent.ConcurrentBag<string>();

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered.Add(device.Sid);
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendCommand(It.IsAny<DiscoverGatewayCommand>())).Returns(0)
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = $"[\"{waterLeakSid}\",\"{thSensorSid}\"]"
            }.ToString());

        _messageTransport
            .Setup(x => x.SendCommand(It.Is<ReadDeviceCommand>(cmd => cmd.Sid == waterLeakSid))).Returns(0)
            .Raises(x => x.OnMessageReceived += null, new Gw2Response { Cmd = "read_ack", Sid = waterLeakSid, Model = "sensor_wleak.aq1", Data = "{\"voltage\":3015}" }.ToString());

        _messageTransport
            .Setup(x => x.SendCommand(It.Is<ReadDeviceCommand>(cmd => cmd.Sid == thSensorSid))).Returns(0)
            .Raises(x => x.OnMessageReceived += null, new Gw2Response { Cmd = "read_ack", Sid = thSensorSid, Model = "sensor_ht", Data = "{\"voltage\":3025}" }.ToString());

        await _gateway.DiscoverDevicesAsync();

        discovered.Should().BeEquivalentTo([waterLeakSid, thSensorSid]);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Throws_When_Gateway_Does_Not_Answer()
    {
        _gateway.DiscoveryTimeout = TimeSpan.FromMilliseconds(200);

        var act = () => _gateway.DiscoverDevicesAsync();

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Does_Not_Block_A_Thread_During_Pauses()
    {
        var sids = Enumerable.Range(0, 3).Select(_ => _fixture.Create<string>()).ToArray();

        _messageTransport
            .Setup(x => x.SendCommand(It.IsAny<DiscoverGatewayCommand>())).Returns(0)
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = JsonSerializer.Serialize(sids)
            }.ToString());

        // Three 100 ms pauses: a blocking wait would hold the caller for at least 300 ms
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var discovery = _gateway.DiscoverDevicesAsync();
        var returnedAfter = sw.ElapsedMilliseconds;

        await discovery;

        returnedAfter.Should().BeLessThan(250, "DiscoverDevicesAsync must return to the caller before the pauses run");
        _messageTransport.Verify(x => x.SendCommand(It.IsAny<ReadDeviceCommand>()), Times.Exactly(sids.Length + 1));
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

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "off", false)]
    public async Task IsArmingOn_Returns_Arming_State(bool isAsync, string reported, bool expected)
    {
        SendResultMethodAsync(string.Empty, [reported]);

        var arming = await Call(isAsync, _gateway.IsArmingOn, _gateway.IsArmingOnAsync);

        VerifyMethodAsync("get_arming", Array.Empty<string>());
        arming.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, true, "on")]
    [InlineData(true, true, "on")]
    [InlineData(false, false, "off")]
    [InlineData(true, false, "off")]
    public async Task SetArming_Sends_Command(bool isAsync, bool on, string expected)
    {
        SendResultMethodAsync(string.Empty, ["ok"]);

        if (on) await Call(isAsync, _gateway.SetArmingOn, _gateway.SetArmingOnAsync);
        else await Call(isAsync, _gateway.SetArmingOff, _gateway.SetArmingOffAsync);

        VerifyMethodAsync("set_arming", [expected]);
    }

    public static TheoryData<bool, string, int, Func<XiaomiGateway2, bool, Task<int>>, object[]> IntGetters => new()
    {
        { false, "wait time", 15, (gw, a) => Call(a, gw.GetArmingWaitTime, gw.GetArmingWaitTimeAsync), ["get_arm_wait_time", Array.Empty<object>()] },
        { true, "wait time", 30, (gw, a) => Call(a, gw.GetArmingWaitTime, gw.GetArmingWaitTimeAsync), ["get_arm_wait_time", Array.Empty<object>()] },
        { false, "off time", 15, (gw, a) => Call(a, gw.GetArmingOffTime, gw.GetArmingOffTimeAsync), ["get_device_prop", new object[] { "lumi.0", "alarm_time_len" }] },
        { true, "off time", 30, (gw, a) => Call(a, gw.GetArmingOffTime, gw.GetArmingOffTimeAsync), ["get_device_prop", new object[] { "lumi.0", "alarm_time_len" }] },
        { false, "blinking time", 15, (gw, a) => Call(a, gw.GetArmingBlinkingTime, gw.GetArmingBlinkingTimeAsync), ["get_device_prop", new object[] { "lumi.0", "en_alarm_light" }] },
        { true, "blinking time", 30, (gw, a) => Call(a, gw.GetArmingBlinkingTime, gw.GetArmingBlinkingTimeAsync), ["get_device_prop", new object[] { "lumi.0", "en_alarm_light" }] },
        { false, "volume", 10, (gw, a) => Call(a, gw.GetArmingVolume, gw.GetArmingVolumeAsync), ["get_alarming_volume", Array.Empty<object>()] },
        { true, "volume", 20, (gw, a) => Call(a, gw.GetArmingVolume, gw.GetArmingVolumeAsync), ["get_alarming_volume", Array.Empty<object>()] },
        { false, "last triggered", 1609150074, (gw, a) => Call(a, gw.GetArmingLastTimeTriggeredTimestamp, gw.GetArmingLastTimeTriggeredTimestampAsync), ["get_arming_time", Array.Empty<object>()] },
        { true, "last triggered", 1609150074, (gw, a) => Call(a, gw.GetArmingLastTimeTriggeredTimestamp, gw.GetArmingLastTimeTriggeredTimestampAsync), ["get_arming_time", Array.Empty<object>()] },
    };

    [Theory]
    [MemberData(nameof(IntGetters))]
    public async Task Arming_Getter_Returns_Integer(bool isAsync, string property, int value, Func<XiaomiGateway2, bool, Task<int>> get, object[] expectedRequest)
    {
        SendResultMethodAsync(string.Empty, [value]);

        var actual = await get(_gateway, isAsync);

        VerifyMethodAsync((string)expectedRequest[0], (object[])expectedRequest[1]);
        actual.Should().Be(value, property);
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 30)]
    public async Task SetArmingWaitTime_Sends_Command(bool isAsync, int seconds)
    {
        SendResultMethodAsync(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingWaitTime(seconds), () => _gateway.SetArmingWaitTimeAsync(seconds));

        VerifyMethodAsync("set_arm_wait_time", [seconds]);
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 45)]
    public async Task SetArmingOffTime_Sends_Command(bool isAsync, int seconds)
    {
        SendResultMethodAsync(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingOffTime(seconds), () => _gateway.SetArmingOffTimeAsync(seconds));

        VerifySetDeviceProp(new { sid = "lumi.0", alarm_time_len = seconds });
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 45)]
    public async Task SetArmingBlinkingTime_Sends_Command(bool isAsync, int seconds)
    {
        SendResultMethodAsync(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingBlinkingTime(seconds), () => _gateway.SetArmingBlinkingTimeAsync(seconds));

        VerifySetDeviceProp(new { sid = "lumi.0", en_alarm_light = seconds });
    }

    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 30)]
    public async Task SetArmingVolume_Sends_Command(bool isAsync, int volume)
    {
        SendResultMethodAsync(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingVolume(volume), () => _gateway.SetArmingVolumeAsync(volume));

        VerifyMethodAsync("set_alarming_volume", [volume]);
    }

    private void VerifySetDeviceProp(object @params) =>
        _miioTransport.Verify(x => x.SendMessageAsync(new { id = 1, method = "set_device_prop", @params }.ToJson()), Times.Once());

    private static readonly int[] DefaultChannels = [1025, 1026, 1027];

    private void SetupRadioChannels(params int[] ids) =>
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson(ids)));

    private static object Channel(int id, string url = null) => new { id, url = url ?? $"http://192.168.1.1/radio{id}.m3u8", type = 0 };

    private void VerifySentSecond(string method, object @params) =>
        _miioTransport.Verify(x => x.SendMessageAsync(new { id = 2, method, @params }.ToJson()), Times.Once());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetRadioChannels_Returns_List_of_RadioChannels(bool isAsync)
    {
        SetupRadioChannels(DefaultChannels);

        var radioChannels = await Call(isAsync, _gateway.GetRadioChannels, _gateway.GetRadioChannelsAsync);

        _miioTransport.Verify(x => x.SendMessageAsync(new { id = 1, method = "get_channels", @params = new { start = 0 } }.ToJson()), Times.Once());
        radioChannels.Should().HaveCount(3);
    }

    [Fact]
    public void AddRadioChannel_with_Id_less_than_1024_throws_exception()
    {
        _gateway.Invoking(x => x.AddRadioChannel(1000, "url here")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRadioChannel_with_existing_Id_throws_exception()
    {
        SetupRadioChannels(1025, 1045, 1027);

        _gateway.Invoking(x => x.AddRadioChannel(1045, "http://192.168.1.1/radio.m3u8")).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddRadioChannel_Sends_Command(bool isAsync)
    {
        const string url = "http://192.168.1.1/radio4.m3u8";
        SetupRadioChannels(DefaultChannels);
        SendResultMethodAsync("add_channels", ["ok"]);

        await Call(isAsync, () => _gateway.AddRadioChannel(1045, url), () => _gateway.AddRadioChannelAsync(1045, url));

        VerifySentSecond("add_channels", new { chs = new[] { Channel(1045, url) } });
    }

    [Fact]
    public void RemoveRadioChannel_Should_Throw_Exception_When_Non_Existing_Id()
    {
        SetupRadioChannels(DefaultChannels);

        _gateway.Invoking(x => x.RemoveRadioChannel(1045)).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false, 1027)]
    [InlineData(true, 1025)]
    public async Task RemoveRadioChannel_Sends_Command(bool isAsync, int channelId)
    {
        SetupRadioChannels(DefaultChannels);
        SendResultMethodAsync("remove_channels", ["ok"]);

        await Call(isAsync, () => _gateway.RemoveRadioChannel(channelId), () => _gateway.RemoveRadioChannelAsync(channelId));

        VerifySentSecond("remove_channels", new { chs = new[] { Channel(channelId) } });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveAllRadioChannels_Sends_Command(bool isAsync)
    {
        SetupRadioChannels(DefaultChannels);
        SendResultMethodAsync("remove_channels", ["ok"]);

        await Call(isAsync, _gateway.RemoveAllRadioChannels, _gateway.RemoveAllRadioChannelsAsync);

        VerifySentSecond("remove_channels", new { chs = new[] { Channel(1025), Channel(1026), Channel(1027) } });
    }

    [Fact]
    public void PlayRadio_Should_Throw_Exception_When_Wrong_Volume()
    {
        _gateway.Invoking(x => x.PlayRadio(1045, 120)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PlayRadio_Should_Throw_Exception_When_Wrong_ChannelId()
    {
        SetupRadioChannels(DefaultChannels);

        _gateway.Invoking(x => x.PlayRadio(1045, 50)).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false, 1027)]
    [InlineData(true, 1025)]
    public async Task PlayRadio_Sends_Command(bool isAsync, int channelId)
    {
        SetupRadioChannels(DefaultChannels);
        SendResultMethodAsync("play_specify_fm", ["ok"]);

        await Call(isAsync, () => _gateway.PlayRadio(channelId, 50), () => _gateway.PlayRadioAsync(channelId, 50));

        VerifySentSecond("play_specify_fm", new[] { channelId, 50 });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopRadio_Sends_Command(bool isAsync)
    {
        SendResultMethodAsync("play_fm", ["ok"]);

        await Call(isAsync, _gateway.StopRadio, _gateway.StopRadioAsync);

        VerifyMethodAsync("play_fm", ["off"]);
    }
}
