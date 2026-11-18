using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.XiaomiGateway2;

public partial class XiaomiGateway2
{
    public void SetDeveloperKey(string key) => RunSync(() => SetDeveloperKeyAsync(key));

    public async Task SetDeveloperKeyAsync(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (key.Length != 16)
        {
            throw new ArgumentException("Developer key must be exactly 16 characters long", nameof(key));
        }


        await _developerKeyLock.WaitAsync(_lifetimeToken).ConfigureAwait(false);
        try
        {
            CheckMessage(await RequestArrayAsync("set_lumi_dpf_aes_key", key).ConfigureAwait(false), "Unable to set developer key");
            _developerKey = key;
        }
        finally { _developerKeyLock.Release(); }
    }

    public string GetDeveloperKey() => RunSync(GetDeveloperKeyAsync);

    public async Task<string> GetDeveloperKeyAsync()
    {
        var response = await RequestArrayAsync("get_lumi_dpf_aes_key").ConfigureAwait(false);
        var json = JsonNode.Parse(response).AsObject();

        if (
                json.ContainsKey("error") ||
                !json.ContainsKey("result") ||
                json["result"].AsArray().Count == 0 ||
                json["result"][0].ToString() == string.Empty)
        {
            throw new InvalidOperationException("Unable to get developer key, please set development mode for your gateway according to the gateway setup instructions in the project wiki");
        }

        return json["result"][0].ToString();
    }

    public void EnableNightLight(byte r, byte g, byte b, int brightness) => RunSync(() => EnableNightLightAsync(r, g, b, brightness));

    public async Task EnableNightLightAsync(byte r, byte g, byte b, int brightness)
    {
        await SetLightAsync("set_night_light_rgb", r, g, b, brightness).ConfigureAwait(false);
    }

    public void DisableNightLight() => RunSync(DisableNightLightAsync);

    public async Task DisableNightLightAsync()
    {
        await SetLightAsync("set_night_light_rgb", 0, 0, 0, 0).ConfigureAwait(false);
    }

    public void EnableLight(byte r, byte g, byte b, int brightness) => RunSync(() => EnableLightAsync(r, g, b, brightness));

    public async Task EnableLightAsync(byte r, byte g, byte b, int brightness)
    {
        await SetLightAsync("set_rgb", r, g, b, brightness).ConfigureAwait(false);
    }

    public void DisableLight() => RunSync(DisableLightAsync);

    public async Task DisableLightAsync()
    {
        await SetLightAsync("set_rgb", 0, 0, 0, 0).ConfigureAwait(false);
    }

    private async Task SetLightAsync(string dayOrNight, byte r, byte g, byte b, int brightness)
    {
        if (brightness < 0 || brightness > 100) throw new ArgumentOutOfRangeException(nameof(brightness), brightness, "Brightness must be in range 0 - 100");

        var rgb = (uint)brightness << 24 | (uint)r << 16 | (uint)g << 8 | b;


        CheckMessage(await RequestArrayAsync(dayOrNight, rgb, brightness).ConfigureAwait(false), $"Unable to send command '{dayOrNight}' with {rgb} and {brightness}");
    }

    public void PlaySound(Sound sound, int volume) => RunSync(() => PlaySoundAsync(sound, volume));

    public async Task PlaySoundAsync(Sound sound, int volume)
    {
        if (volume < 0 || volume > 100) throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be in range 0-100");
        CheckMessage(await RequestArrayAsync("play_music_new", ((int)sound).ToString(), volume).ConfigureAwait(false), $"Unable to play sound {sound}");
    }

    public void SoundsOff() => RunSync(SoundsOffAsync);

    public async Task SoundsOffAsync()
    {
        await PlaySoundAsync(0, 0).ConfigureAwait(false);
    }

    public void PlayCustomSound(int soundNo, int volume) => RunSync(() => PlayCustomSoundAsync(soundNo, volume));
    public async Task PlayCustomSoundAsync(int soundNo, int volume)
    {
        if (soundNo <= 10_000) throw new ArgumentOutOfRangeException(nameof(soundNo), soundNo, "Custom sounds must start from 10001");
        if (volume < 0 || volume > 100) throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be in range 0-100");

        CheckMessage(await RequestArrayAsync("play_music_new", soundNo.ToString(), volume).ConfigureAwait(false), $"Unable to play custom sound {soundNo}");
    }

    public bool IsArmingOn() => RunSync(IsArmingOnAsync);

    public async Task<bool> IsArmingOnAsync()
    {
        var msg = await RequestArrayAsync("get_arming", []).ConfigureAwait(false);

        return CheckArmingState(msg);
    }

    private bool CheckArmingState(string msg)
    {
        string[] _armingStates = ["on", "off"];

        var result = JsonNode.Parse(msg)["result"][0].ToString();

        if (!_armingStates.Contains(result))
        {
            throw new InvalidOperationException($"Arming state is unknown, looks like miio protocol error --> '{msg}'");
        }
        ;

        return result == "on";
    }

    public void SetArmingOff() => RunSync(SetArmingOffAsync);

    public async Task SetArmingOffAsync()
    {
        var result = await RequestArrayAsync("set_arming", "off").ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming off");
    }

    public void SetArmingOn() => RunSync(SetArmingOnAsync);

    public async Task SetArmingOnAsync()
    {
        var result = await RequestArrayAsync("set_arming", "on").ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming on");
    }

    public int GetArmingWaitTime() => RunSync(GetArmingWaitTimeAsync);

    public async Task<int> GetArmingWaitTimeAsync()
    {
        var result = await RequestArrayAsync("get_arm_wait_time", new string[0]).ConfigureAwait(false);
        return GetInteger(result, "Unable to get arming wait time");
    }


    public void SetArmingWaitTime(int seconds) => RunSync(() => SetArmingWaitTimeAsync(seconds));

    public async Task SetArmingWaitTimeAsync(int seconds)
    {
        var result = await RequestArrayAsync("set_arm_wait_time", seconds).ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming wait time");
    }

    public int GetArmingOffTime() => RunSync(GetArmingOffTimeAsync);

    public async Task<int> GetArmingOffTimeAsync()
    {
        var result = await RequestArrayAsync("get_device_prop", "lumi.0", "alarm_time_len").ConfigureAwait(false);
        return GetInteger(result, "Unable to get arming off time");
    }


    public void SetArmingOffTime(int seconds) => RunSync(() => SetArmingOffTimeAsync(seconds));

    public async Task SetArmingOffTimeAsync(int seconds)
    {
        var result = await RequestSidPropAsync("set_device_prop", "lumi.0", "alarm_time_len", seconds).ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming off time");
    }

    public int GetArmingBlinkingTime() => RunSync(GetArmingBlinkingTimeAsync);

    public async Task<int> GetArmingBlinkingTimeAsync()
    {
        var result = await RequestArrayAsync("get_device_prop", "lumi.0", "en_alarm_light").ConfigureAwait(false);
        return GetInteger(result, "Unable to get arming blinking time");
    }


    public void SetArmingBlinkingTime(int seconds) => RunSync(() => SetArmingBlinkingTimeAsync(seconds));

    public async Task SetArmingBlinkingTimeAsync(int seconds)
    {
        var result = await RequestSidPropAsync("set_device_prop", "lumi.0", "en_alarm_light", seconds).ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming blinking time");
    }

    public int GetArmingVolume() => RunSync(GetArmingVolumeAsync);

    public async Task<int> GetArmingVolumeAsync()
    {
        var result = await RequestArrayAsync("get_alarming_volume", []).ConfigureAwait(false);
        return GetInteger(result, "Unable to get arming volume level");
    }


    public void SetArmingVolume(int volume) => RunSync(() => SetArmingVolumeAsync(volume));

    public async Task SetArmingVolumeAsync(int volume)
    {
        var result = await RequestArrayAsync("set_alarming_volume", volume).ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming volume level");
    }

    /// <summary>
    /// Get last time when alarm was triggered unix timestamp
    /// </summary>
    /// <returns>unix timestamp seconds</returns>
    public int GetArmingLastTimeTriggeredTimestamp() => RunSync(GetArmingLastTimeTriggeredTimestampAsync);

    /// <summary>
    /// Get last time when alarm was triggered unix timestamp
    /// </summary>
    /// <returns>unix timestamp seconds</returns>
    public async Task<int> GetArmingLastTimeTriggeredTimestampAsync()
    {
        var result = await RequestArrayAsync("get_arming_time", []).ConfigureAwait(false);
        return GetInteger(result, "Unable to get timestamp when arming was triggered");
    }


}
