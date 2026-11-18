using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static MiHomeLib.MiioDevices.XiaomiRobotVacuumX20Plus;

namespace MiHomeUnitTests.Devices.Miio;

public partial class XiaomiRobotVacuumX20PlusTests
{
    public static IEnumerable<object[]> SettingCases()
    {
        foreach (var isAsync in new[] { false, true })
        {
            yield return [isAsync, "suction", 2, 3, 3];
            yield return [isAsync, "water", 4, 5, 2];
            yield return [isAsync, "resume", 4, 11, 1];
            yield return [isAsync, "carpet", 4, 12, 0];
            yield return [isAsync, "lock", 4, 27, 1];
            yield return [isAsync, "mop-reminder", 4, 16, 120];
            yield return [isAsync, "volume", 7, 1, 0];
            yield return [isAsync, "dnd", 5, 1, true];
            yield return [isAsync, "dnd-start", 5, 2, "22:30"];
            yield return [isAsync, "dnd-end", 5, 3, "08:00"];
            yield return [isAsync, "voice", 7, 2, "20"];
            yield return [isAsync, "schedule", 8, 2, "1-0-20:38-0000000-0-2-3-32-2"];
            yield return [isAsync, "switch", 4, 50, "{\"k\":\"MeticulousTwist\",\"v\":-7}"];
        }
    }

    [Theory]
    [MemberData(nameof(SettingCases))]
    public async Task Settings_Write_Expected_Property_And_Value(bool isAsync, string setting, int service, int property, object value)
    {
        SetupSetProperties();
        (Action Sync, Func<Task> Async) call = setting switch
        {
            "suction" => (() => _miRobot.SetSuctionMode(SuctionMode.Max), () => _miRobot.SetSuctionModeAsync(SuctionMode.Max)),
            "water" => (() => _miRobot.SetWaterLevel(WaterLevel.Medium), () => _miRobot.SetWaterLevelAsync(WaterLevel.Medium)),
            "resume" => (() => _miRobot.SetResumeAfterCharging(true), () => _miRobot.SetResumeAfterChargingAsync(true)),
            "carpet" => (() => _miRobot.SetCarpetBoost(false), () => _miRobot.SetCarpetBoostAsync(false)),
            "lock" => (() => _miRobot.SetChildLock(true), () => _miRobot.SetChildLockAsync(true)),
            "mop-reminder" => (() => _miRobot.SetMopWashReminder(120), () => _miRobot.SetMopWashReminderAsync(120)),
            "volume" => (() => _miRobot.SetVolume(0), () => _miRobot.SetVolumeAsync(0)),
            "dnd" => (() => _miRobot.SetDndEnabled(true), () => _miRobot.SetDndEnabledAsync(true)),
            "dnd-start" => (() => _miRobot.SetDndStartTime(new TimeSpan(22, 30, 0)), () => _miRobot.SetDndStartTimeAsync(new TimeSpan(22, 30, 0))),
            "dnd-end" => (() => _miRobot.SetDndEndTime(new TimeSpan(8, 0, 0)), () => _miRobot.SetDndEndTimeAsync(new TimeSpan(8, 0, 0))),
            "voice" => (() => _miRobot.SetVoicePacket("20"), () => _miRobot.SetVoicePacketAsync("20")),
            "schedule" => (() => _miRobot.SetCleaningSchedule((string)value), () => _miRobot.SetCleaningScheduleAsync((string)value)),
            "switch" => (() => _miRobot.SetExtendedSwitch("MeticulousTwist", -7), () => _miRobot.SetExtendedSwitchAsync("MeticulousTwist", -7)),
            _ => throw new ArgumentOutOfRangeException(nameof(setting))
        };

        await Call(isAsync, call.Sync, call.Async);

        VerifySetProperties(service, property, value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task Volume_Accepts_Both_Bounds(int volume)
    {
        SetupSetProperties();

        await _miRobot.SetVolumeAsync(volume);

        VerifySetProperties(7, 1, volume);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Invalid_Volume_Does_Not_Send_Request(int volume)
    {
        Func<Task> set = () => _miRobot.SetVolumeAsync(volume);

        await set.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public async Task Invalid_Mop_Reminder_Does_Not_Send_Request(int minutes)
    {
        Func<Task> set = () => _miRobot.SetMopWashReminderAsync(minutes);

        await set.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Voice_Package_Installation_Sends_Metadata_As_JSON_String(bool isAsync)
    {
        SetupSetProperties();
        const string url = "http://192.0.2.1/voice.pkg";
        const string md5 = "0123456789abcdef0123456789abcdef";

        await Call(isAsync, () => _miRobot.InstallVoicePackage("20", url, md5, 1024),
            () => _miRobot.InstallVoicePackageAsync("20", url, md5, 1024));

        VerifySetProperties(7, 4, (object)JsonSerializer.Serialize(new { id = "20", url, md5, size = 1024 }));
    }

    [Theory]
    [InlineData(null, "http://192.0.2.1/voice.pkg", "0123456789abcdef0123456789abcdef", 1024)]
    [InlineData("20", "relative/path", "0123456789abcdef0123456789abcdef", 1024)]
    [InlineData("20", "file:///voice.pkg", "0123456789abcdef0123456789abcdef", 1024)]
    [InlineData("20", "http://192.0.2.1/voice.pkg", "bad", 1024)]
    [InlineData("20", "http://192.0.2.1/voice.pkg", "0123456789abcdef0123456789abcdef", 0)]
    public async Task Invalid_Voice_Package_Metadata_Does_Not_Send_Request(string packetId, string url, string md5, int size)
    {
        Func<Task> install = () => _miRobot.InstallVoicePackageAsync(packetId, url, md5, size);

        await install.Should().ThrowAsync<ArgumentException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Invalid_Suction_Does_Not_Send_Request(int mode)
    {
        Func<Task> set = () => _miRobot.SetSuctionModeAsync((SuctionMode)mode);

        await set.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Invalid_Water_Level_Does_Not_Send_Request(int level)
    {
        Func<Task> set = () => _miRobot.SetWaterLevelAsync((WaterLevel)level);

        await set.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86400)]
    [InlineData(30)]
    public async Task Dnd_Rejects_Times_Outside_Day_Or_Without_Minute_Precision(int seconds)
    {
        Func<Task> start = () => _miRobot.SetDndStartTimeAsync(TimeSpan.FromSeconds(seconds));
        Func<Task> end = () => _miRobot.SetDndEndTimeAsync(TimeSpan.FromSeconds(seconds));

        await start.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await end.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Empty_Identifiers_Do_Not_Send_Requests(string value)
    {
        Func<Task> voice = () => _miRobot.SetVoicePacketAsync(value);
        Func<Task> extendedSwitch = () => _miRobot.SetExtendedSwitchAsync(value, 1);
        Func<Task> deleteSchedule = () => _miRobot.DeleteCleaningScheduleAsync(value);

        await voice.Should().ThrowAsync<ArgumentException>();
        await extendedSwitch.Should().ThrowAsync<ArgumentException>();
        await deleteSchedule.Should().ThrowAsync<ArgumentException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Null_Schedule_Is_Rejected_And_Empty_Schedule_Is_Preserved()
    {
        Func<Task> set = () => _miRobot.SetCleaningScheduleAsync(null);
        await set.Should().ThrowAsync<ArgumentNullException>();
        SetupSetProperties();

        await _miRobot.SetCleaningScheduleAsync("");

        VerifySetProperties(8, 2, "");
    }

    [Theory]
    [InlineData(false, "{\"id\":\"\",\"state\":\"idle\",\"progress\":0}")]
    [InlineData(true, "{\"id\":\"\",\"state\":\"idle\",\"progress\":0}")]
    [InlineData(true, "{\\\"id\\\":\\\"\\\",\\\"state\\\":\\\"idle\\\",\\\"progress\\\":0}")]
    [InlineData(true, "\"{\\\"state\\\":\\\"idle\\\",\\\"progress\\\":0}\"")]
    public async Task Voice_State_Handles_Firmware_JSON_Encodings(bool isAsync, string data)
    {
        SetupGetProperties((7, 2, "20"), (7, 3, data));

        var result = await Call(isAsync, _miRobot.GetVoiceSettings, _miRobot.GetVoiceSettingsAsync);

        result.Should().Be(new VoiceSettings("20", "idle", 0));
        VerifyGetProperties(2, (7, 2), (7, 3));
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"state\":\"idle\",\"progress\":\"bad\"}")]
    public async Task Malformed_Voice_State_Has_Context(string data)
    {
        SetupGetProperties((7, 2, "20"), (7, 3, data));

        Func<Task> read = () => _miRobot.GetVoiceSettingsAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*voice state (7/3)*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Extended_Switches_Preserve_Unknown_Keys_And_Value_Types_After_Disposal(bool isAsync)
    {
        SetupGetProperties((4, 50, "[{\"k\":\"MeticulousTwist\",\"v\":-7},{\"k\":\"Future\",\"v\":{\"enabled\":true}}]"));

        var result = await Call(isAsync, _miRobot.GetExtendedSwitches, _miRobot.GetExtendedSwitchesAsync);

        result["MeticulousTwist"].GetInt32().Should().Be(-7);
        result["Future"].GetProperty("enabled").GetBoolean().Should().BeTrue();
        Action mutate = () => ((IDictionary<string, JsonElement>)result).Clear();
        mutate.Should().Throw<NotSupportedException>();
        VerifyGetProperties(4, 50);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[{\"k\":\"\",\"v\":1}]")]
    [InlineData("[{\"k\":\"A\",\"v\":1},{\"k\":\"A\",\"v\":2}]")]
    public async Task Malformed_Extended_Switches_Have_Context(string data)
    {
        SetupGetProperties((4, 50, data));

        Func<Task> read = () => _miRobot.GetExtendedSwitchesAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*extended switches (4/50)*");
    }
}
