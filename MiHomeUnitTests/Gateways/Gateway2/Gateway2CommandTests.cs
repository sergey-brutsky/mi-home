using MiHomeLib.Protocols;
using MiHomeLib.MiioDevices;
using MiHomeLib.Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using MiHomeLib.XiaomiGateway2;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using MiHomeLib;
using UnifiedTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;
using UnifiedAqaraTemperatureHumiditySensor = MiHomeLib.Devices.AqaraTemperatureHumiditySensor;
using UnifiedXiaomiSmartWirelessSwitch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;
using UnifiedXiaomiBluetoothHygrothermograph2 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph2;
using UnifiedAqaraWaterLeakSensor = MiHomeLib.Devices.AqaraWaterLeakSensor;

namespace MiHomeUnitTests.Gateways.Gateway2;

public class Gateway2CommandTests : Gateway2TestBase
{
    [Fact]
    public async Task Write_Waits_For_A_Key_Change_And_Uses_The_New_Key()
    {
        var writer = (IGateway2Writer)_gateway;
        await writer.WriteAsync("initial", "plug", ("status", "on"));
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("set_lumi_dpf_aes_key"))))
            .Returns(response.Task);

        var change = _gateway.SetDeveloperKeyAsync("0123456789abcdef");
        var write = writer.WriteAsync("waiting", "plug", ("status", "on"));
        try
        {
            write.IsCompleted.Should().BeFalse();
            _messageTransport.Verify(transport => transport.SendWrite("waiting", "plug", It.IsAny<string>(), It.IsAny<(string, string)[]>()), Times.Never);
        }
        finally
        {
            response.TrySetResult("{\"result\":[\"ok\"]}");
            await Task.WhenAll(change, write).WaitAsync(TimeSpan.FromSeconds(2));
        }

        _messageTransport.Verify(transport => transport.SendWrite("waiting", "plug", "0123456789abcdef", It.IsAny<(string, string)[]>()), Times.Once);
    }

    [Fact]
    public async Task Key_Change_Waits_For_An_Active_Udp_Write()
    {
        var writer = (IGateway2Writer)_gateway;
        await writer.WriteAsync("initial", "plug", ("status", "on"));
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new System.Threading.ManualResetEventSlim();
        _messageTransport.Setup(transport => transport.SendWrite("active", "plug", It.IsAny<string>(), It.IsAny<(string, string)[]>()))
            .Callback(() =>
            {
                entered.TrySetResult(true);
                release.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
            });
        SendResultMethod("set_lumi_dpf_aes_key", "ok");
        var write = Task.Run(() => writer.WriteAsync("active", "plug", ("status", "on")));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task change = null;
        try
        {
            change = _gateway.SetDeveloperKeyAsync("0123456789abcdef");
            _miioTransport.Verify(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("set_lumi_dpf_aes_key"))), Times.Never);
        }
        finally
        {
            release.Set();
            await write.WaitAsync(TimeSpan.FromSeconds(2));
            if (change is not null) await change.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task Failed_Key_Change_Preserves_The_Cache_And_Releases_The_Writer()
    {
        var writer = (IGateway2Writer)_gateway;
        await writer.WriteAsync("initial", "plug", ("status", "on"));
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("set_lumi_dpf_aes_key"))))
            .ReturnsAsync("{\"error\":{\"code\":-1,\"message\":\"failed\"}}");
        Func<Task> change = () => _gateway.SetDeveloperKeyAsync("0123456789abcdef");

        await change.Should().ThrowAsync<InvalidOperationException>();
        await writer.WriteAsync("after-failure", "plug", ("status", "on")).WaitAsync(TimeSpan.FromSeconds(2));

        DeveloperKeyRequests().Should().Be(1);
        _messageTransport.Verify(transport => transport.SendWrite("after-failure", "plug", "f40e1b285fes68cd", It.IsAny<(string, string)[]>()), Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Invalid_Command_Ranges_Identify_The_Parameter(int value)
    {
        var light = _gateway.Invoking(gateway => gateway.EnableLight(0, 0, 0, value))
            .Should().Throw<ArgumentOutOfRangeException>().Which;
        light.ParamName.Should().Be("brightness");
        light.ActualValue.Should().Be(value);
        var sound = _gateway.Invoking(gateway => gateway.PlaySound(0, value))
            .Should().Throw<ArgumentOutOfRangeException>().Which;
        sound.ParamName.Should().Be("volume");
        sound.ActualValue.Should().Be(value);
        var radio = _gateway.Invoking(gateway => gateway.PlayRadio(1024, value))
            .Should().Throw<ArgumentOutOfRangeException>().Which;
        radio.ParamName.Should().Be("volume");
        radio.ActualValue.Should().Be(value);
    }

    [Fact]
    public async Task Concurrent_First_Writes_Request_The_Developer_Key_Once()
    {
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("get_lumi_dpf_aes_key"))))
            .Returns(response.Task);
        var writer = (IGateway2Writer)_gateway;

        var first = writer.WriteAsync("plug-1", "plug", ("status", "on"));
        var second = writer.WriteAsync("plug-2", "plug", ("status", "off"));
        DeveloperKeyRequests().Should().Be(1);
        response.SetResult("{\"result\":[\"f40e1b285fes68cd\"]}");
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));

        DeveloperKeyRequests().Should().Be(1);
        _messageTransport.Verify(transport => transport.SendWrite("plug-1", "plug", "f40e1b285fes68cd", It.IsAny<(string, string)[]>()), Times.Once);
        _messageTransport.Verify(transport => transport.SendWrite("plug-2", "plug", "f40e1b285fes68cd", It.IsAny<(string, string)[]>()), Times.Once);
    }

    [Fact]
    public void Constructor_Does_Not_Request_Developer_Key()
    {
        DeveloperKeyRequests().Should().Be(0, "the key is needed only for write commands");
    }

    [Fact]
    public async Task Developer_Key_Is_Requested_Once_On_First_Write_And_Cached()
    {
        var writer = (MiHomeLib.XiaomiGateway2.IGateway2Writer)_gateway;

        await writer.WriteAsync("plug-1", "plug", ("status", "on"));
        await writer.WriteAsync("plug-1", "plug", ("status", "off"));

        DeveloperKeyRequests().Should().Be(1);
        _messageTransport.Verify(x => x.SendWrite("plug-1", "plug", "f40e1b285fes68cd", It.IsAny<(string, string)[]>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Failed_Developer_Key_Request_Is_Not_Cached()
    {
        _miioTransport.Reset();
        _miioTransport.SetupSequence(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_lumi_dpf_aes_key"))))
            .Returns(Task.FromResult("{\"result\":[\"\"],\"id\":1}"))
            .Returns(Task.FromResult("{\"result\":[\"f40e1b285fes68cd\"],\"id\":2}"));
        var writer = (MiHomeLib.XiaomiGateway2.IGateway2Writer)_gateway;

        Func<Task> write = () => writer.WriteAsync("plug-1", "plug", ("status", "on"));
        await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("*developer key*");
        await writer.WriteAsync("plug-1", "plug", ("status", "on"));

        DeveloperKeyRequests().Should().Be(2);
        _messageTransport.Verify(x => x.SendWrite("plug-1", "plug", "f40e1b285fes68cd", It.IsAny<(string, string)[]>()), Times.Once);
    }

    [Fact]
    public async Task SetDeveloperKey_Replaces_The_Cached_Key_Without_A_Request()
    {
        SendResultMethod("set_lumi_dpf_aes_key", "ok");
        await _gateway.SetDeveloperKeyAsync("0123456789abcdef");

        await ((MiHomeLib.XiaomiGateway2.IGateway2Writer)_gateway).WriteAsync("plug-1", "plug", ("status", "on"));

        DeveloperKeyRequests().Should().Be(0);
        _messageTransport.Verify(x => x.SendWrite("plug-1", "plug", "0123456789abcdef", It.IsAny<(string, string)[]>()), Times.Once);
    }

    [Theory]
    [InlineData("day-on", "set_rgb", 16909060, 1)]
    [InlineData("day-off", "set_rgb", 0, 0)]
    [InlineData("night-on", "set_night_light_rgb", 16909060, 1)]
    [InlineData("night-off", "set_night_light_rgb", 0, 0)]
    public void Sync_Light_Commands_Await_And_Send_Packed_Color(string operation, string method, uint rgb, int brightness)
    {
        SendResultMethod(method, "ok");
        switch (operation)
        {
            case "day-on": _gateway.EnableLight(2, 3, 4, 1); break;
            case "day-off": _gateway.DisableLight(); break;
            case "night-on": _gateway.EnableNightLight(2, 3, 4, 1); break;
            case "night-off": _gateway.DisableNightLight(); break;
        }
        VerifyMethod(method, rgb, brightness);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sync_Sound_Command_Sends_Expected_Sound(bool custom)
    {
        SendResultMethod("play_music_new", "ok");
        if (custom) _gateway.PlayCustomSound(10001, 50);
        else _gateway.PlaySound(XiaomiGateway2.Sound.Doorbell, 50);
        VerifyMethod("play_music_new", custom ? "10001" : "10", 50);
    }

    [Fact]
    public void Radio_Channel_Equality_Hash_And_Description_Use_Id_And_Url()
    {
        var channel = new XiaomiGateway2.RadioChannel { Id = 1234, Url = "http://radio", Type = 0 };
        var equal = new XiaomiGateway2.RadioChannel { Id = 1234, Url = "http://radio", Type = 1 };
        channel.Equals(null).Should().BeFalse();
        channel.Equals(channel).Should().BeTrue();
        channel.Equals(new object()).Should().BeFalse();
        channel.Equals(equal).Should().BeTrue();
        channel.GetHashCode().Should().Be(equal.GetHashCode());
        channel.Equals(new XiaomiGateway2.RadioChannel { Id = 1235, Url = "http://radio" }).Should().BeFalse();
        channel.ToString().Should().Contain("1234").And.Contain("http://radio");
        _gateway.ToString().Should().Contain(XiaomiGateway2.MARKET_MODEL);
    }

    [Fact]
    public async Task Changed_Developer_Key_Is_Used_By_Existing_Plug()
    {
        SetupIdList("plug-test");
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers("plug-test", ReadAck("plug-test", "plug", "{\"status\":\"off\"}"), 1);
        MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN plug = null;
        _gateway.OnDeviceDiscoveredAsync += d => { plug = (MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN)d; return Task.CompletedTask; };
        await _gateway.DiscoverDevicesAsync();
        SendResultMethod("set_lumi_dpf_aes_key", "ok");
        await _gateway.SetDeveloperKeyAsync("0123456789abcdef");
        plug.PowerOn();
        _messageTransport.Verify(x => x.SendWrite("plug-test", "plug", "0123456789abcdef", It.IsAny<(string, string)[]>()), Times.Once);
        plug.LastTimeMessageReceived.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void SetDeveloperKeyOfWrongLength_Should_Throw_Exceptions()
    {
        // Arrange
        var key = _fixture.Create<string>()[..3]; // key of length 3 charactes

        SendResultMethod("set_lumi_dpf_aes_key", "ok");

        // Act
        var actual = _gateway.Invoking(x => x.SetDeveloperKey(key));

        // Assert
        actual
            .Should()
            .Throw<ArgumentException>()
            .WithMessage("Developer key must be exactly 16 characters long*")
            .Which.ParamName.Should().Be("key");
    }

    [Fact]
    public async Task GetDeveloperKey_Returns_CorrectKey()
    {
        // Arrange
        SendResultMethod(string.Empty, ["f40e1b285fes68cd"]);

        // Act
        var key = await _gateway.GetDeveloperKeyAsync();

        // Assert
        VerifyMethod("get_lumi_dpf_aes_key", Array.Empty<string>());
        key.Should().Be("f40e1b285fes68cd");
    }

    [Fact]
    public void PlaySound_Should_PlaySound_Successfully()
    {
        // Arrange
        var sound = XiaomiGateway2.Sound.Mimix;
        var volume = 3;

        SendResultMethod("play_music_new", "ok");

        // Act
        _ = _gateway.PlaySoundAsync(sound, volume);

        // Assert
        VerifyMethod("play_music_new", ((int)sound).ToString(), volume);
    }

    [Fact]
    public void SoundsOff_Should_Stop_Playing_Sound()
    {
        // Arrange
        SendResultMethod("play_music_new", "ok");

        // Act
        _gateway.SoundsOff();

        // Assert
        VerifyMethod("play_music_new", "0", 0);
    }

    [Fact]
    public void PlayCustomSound_Should_Play_CustomSound_Successfully()
    {
        // Arrange
        var sound = 10_001;
        var volume = 3;

        SendResultMethod("play_music_new", "ok");

        // Act
        _ = _gateway.PlayCustomSoundAsync(sound, volume);

        // Assert
        VerifyMethod("play_music_new", sound.ToString(), volume);
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "off", false)]
    public async Task IsArmingOn_Returns_Arming_State(bool isAsync, string reported, bool expected)
    {
        SendResultMethod(string.Empty, [reported]);

        var arming = await Call(isAsync, _gateway.IsArmingOn, _gateway.IsArmingOnAsync);

        VerifyMethod("get_arming", Array.Empty<string>());
        arming.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, true, "on")]
    [InlineData(true, true, "on")]
    [InlineData(false, false, "off")]
    [InlineData(true, false, "off")]
    public async Task SetArming_Sends_Command(bool isAsync, bool on, string expected)
    {
        SendResultMethod(string.Empty, ["ok"]);

        if (on) await Call(isAsync, _gateway.SetArmingOn, _gateway.SetArmingOnAsync);
        else await Call(isAsync, _gateway.SetArmingOff, _gateway.SetArmingOffAsync);

        VerifyMethod("set_arming", [expected]);
    }

    [Theory]
    [MemberData(nameof(IntGetters))]
    public async Task Arming_Getter_Returns_Integer(bool isAsync, string property, int value, Func<XiaomiGateway2, bool, Task<int>> get, object[] expectedRequest)
    {
        SendResultMethod(string.Empty, [value]);

        var actual = await get(_gateway, isAsync);

        VerifyMethod((string)expectedRequest[0], (object[])expectedRequest[1]);
        actual.Should().Be(value, property);
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 30)]
    public async Task SetArmingWaitTime_Sends_Command(bool isAsync, int seconds)
    {
        SendResultMethod(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingWaitTime(seconds), () => _gateway.SetArmingWaitTimeAsync(seconds));

        VerifyMethod("set_arm_wait_time", [seconds]);
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 45)]
    public async Task SetArmingOffTime_Sends_Command(bool isAsync, int seconds)
    {
        SendResultMethod(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingOffTime(seconds), () => _gateway.SetArmingOffTimeAsync(seconds));

        VerifySetDeviceProp(new { sid = "lumi.0", alarm_time_len = seconds });
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 45)]
    public async Task SetArmingBlinkingTime_Sends_Command(bool isAsync, int seconds)
    {
        SendResultMethod(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingBlinkingTime(seconds), () => _gateway.SetArmingBlinkingTimeAsync(seconds));

        VerifySetDeviceProp(new { sid = "lumi.0", en_alarm_light = seconds });
    }

    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 30)]
    public async Task SetArmingVolume_Sends_Command(bool isAsync, int volume)
    {
        SendResultMethod(string.Empty, ["ok"]);

        await Call(isAsync, () => _gateway.SetArmingVolume(volume), () => _gateway.SetArmingVolumeAsync(volume));

        VerifyMethod("set_alarming_volume", [volume]);
    }

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
        SendResultMethod("add_channels", ["ok"]);

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
        SendResultMethod("remove_channels", ["ok"]);

        await Call(isAsync, () => _gateway.RemoveRadioChannel(channelId), () => _gateway.RemoveRadioChannelAsync(channelId));

        VerifySentSecond("remove_channels", new { chs = new[] { Channel(channelId) } });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveAllRadioChannels_Sends_Command(bool isAsync)
    {
        SetupRadioChannels(DefaultChannels);
        SendResultMethod("remove_channels", ["ok"]);

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
        SendResultMethod("play_specify_fm", ["ok"]);

        await Call(isAsync, () => _gateway.PlayRadio(channelId, 50), () => _gateway.PlayRadioAsync(channelId, 50));

        VerifySentSecond("play_specify_fm", new[] { channelId, 50 });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopRadio_Sends_Command(bool isAsync)
    {
        SendResultMethod("play_fm", ["ok"]);

        await Call(isAsync, _gateway.StopRadio, _gateway.StopRadioAsync);

        VerifyMethod("play_fm", ["off"]);
    }
}
