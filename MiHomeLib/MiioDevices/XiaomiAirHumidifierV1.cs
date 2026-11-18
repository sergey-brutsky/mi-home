using MiHomeLib.MiioDevices.Internal;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices;

public class XiaomiAirHumidifierV1 : MiioDevice
{
    public const string MARKET_MODEL = "CJJSQ01ZM";
    public const string MODEL = "zhimi.humidifier.v1";

    public enum Mode
    {
        Silent,
        Medium,
        High
    };

    public enum Brightness
    {
        Bright = 0,
        Dim = 1,
        Off = 2,
    };

    private readonly Dictionary<string, Mode> _mapMode = new Dictionary<string, Mode>
    {
        { "high", Mode.High},
        { "medium", Mode.Medium},
        { "silent", Mode.Silent},
    };

    [Obsolete("Use MiioDiscovery.DiscoverAsync and filter by DeviceType \"0404\".")]
    public static event Action<string, string, string, string> OnDiscovered;

    public XiaomiAirHumidifierV1(string ip, string token) : base(new MiioTransport(ip, token)) { }

    internal XiaomiAirHumidifierV1(IMiioTransport transport) : base(transport) { }

    /// <summary>A snapshot returned by one get_prop request. Temperature is in degrees Celsius.</summary>
    public sealed record DeviceStatus(bool IsTurnedOn, Mode Mode, float Temperature, int Humidity,
        Brightness LedBrightness, bool IsBuzzerOn, bool IsChildLockOn, int TargetHumidity);

    /// <summary>Reads the current status in one request without retaining a cached state.</summary>
    public DeviceStatus GetStatus() => RunSync(GetStatusAsync);

    /// <inheritdoc cref="GetStatus"/>
    public async Task<DeviceStatus> GetStatusAsync()
    {
        var values = await GetPropsAsync("power", "mode", "temp_dec", "humidity", "led_b", "buzzer", "child_lock", "limit_hum").ConfigureAwait(false);
        const string operation = "Unable to read humidifier status";
        int Number(int index, string field) => MiioResponseReader.ConvertValue(
            () => int.Parse(values[index], CultureInfo.InvariantCulture), $"{operation} {field}");
        bool Switch(int index, string field) => values[index] switch
        {
            "on" => true,
            "off" => false,
            _ => throw new InvalidOperationException($"{operation} {field}: expected 'on' or 'off'.")
        };
        if (!_mapMode.TryGetValue(values[1], out var mode))
            throw new InvalidOperationException($"{operation} mode: unknown mode.");
        return new DeviceStatus(Switch(0, "power"), mode, Number(2, "temp_dec") / 10f,
            Number(3, "humidity"), (Brightness)Number(4, "led_b"), Switch(5, "buzzer"),
            Switch(6, "child_lock"), Number(7, "limit_hum"));
    }

    public override string ToString()
    {
        var values = GetProps("power", "mode", "temp_dec", "humidity", "led_b", "buzzer", "child_lock", "limit_hum");
        var temperature = int.Parse(values[2], CultureInfo.InvariantCulture) / 10f;
        var temp = temperature.ToString("F1", CultureInfo.InvariantCulture) + " °C";
        var brightness = ((Brightness)Enum.Parse(typeof(Brightness), values[4])).ToString().ToLower();

        return $"Power: {values[0]}\nMode: {values[1]}\nTemperature: {temp}\n" +
            $"Humidity: {values[3]}%\nLED brightness: {brightness}\n" +
            $"Buzzer: {values[5]}\nChild lock: {values[6]}\nTarget humidity: {values[7]}%\n" +
            $"Model: {MODEL}\nIP Address:{_miioTransport.Ip}";
    }

    [Obsolete("Use MiioDiscovery.DiscoverAsync and filter by DeviceType \"0404\".")]
    public static void DiscoverDevices()
    {
        var devices = RunSync(() => MiioDiscovery.DiscoverAsync());
        foreach (var device in devices.Where(device => device.DeviceType == "0404"))
            OnDiscovered?.Invoke(device.Ip, device.DeviceType, device.Serial, device.Token);
    }

    public bool GetPowerState() => RunSync(() => GetPowerStateAsync());

    public async Task<bool> GetPowerStateAsync()
    {
        return await ReadPropertyValueAsync("power", MiioResponseReader.OnOff).ConfigureAwait(false);
    }

    public void TurnOn() => RunSync(() => TurnOnAsync());

    public async Task TurnOnAsync()
    {
        var response = await RequestArrayAsync("set_power", "on").ConfigureAwait(false);
        CheckMessage(response, "Unable to power on air humidifier");
    }

    public void TurnOff() => RunSync(() => TurnOffAsync());

    public async Task TurnOffAsync()
    {
        var response = await RequestArrayAsync("set_power", "off").ConfigureAwait(false);
        CheckMessage(response, "Unable to power off air humidifier");
    }

    public Mode GetMode() => RunSync(() => GetModeAsync());

    public async Task<Mode> GetModeAsync()
    {
        return await ReadPropertyValueAsync("mode", value => _mapMode.TryGetValue(value, out var mode)
            ? mode : throw new FormatException("Unknown humidifier mode.")).ConfigureAwait(false);
    }

    public void SetMode(Mode mode) => RunSync(() => SetModeAsync(mode));

    public async Task SetModeAsync(Mode mode)
    {
        ValidateEnum(mode, nameof(mode));
        var response = await RequestArrayAsync("set_mode", mode.ToString().ToLower()).ConfigureAwait(false);
        CheckMessage(response, "Unable to set fan mode of air humidifier");
    }

    public float GetTemperature() => RunSync(() => GetTemperatureAsync());

    public async Task<float> GetTemperatureAsync()
    {
        return (await ReadPropertyValueAsync("temp_dec", value => int.Parse(value, CultureInfo.InvariantCulture)).ConfigureAwait(false)) / 10f;
    }

    public int GetHumidity() => RunSync(() => GetHumidityAsync());

    public async Task<int> GetHumidityAsync()
    {
        return await ReadPropertyValueAsync("humidity", value => int.Parse(value, CultureInfo.InvariantCulture)).ConfigureAwait(false);
    }

    public Brightness GetBrightness() => RunSync(() => GetBrightnessAsync());

    public async Task<Brightness> GetBrightnessAsync()
    {
        return await ReadPropertyValueAsync("led_b", value => Enum.TryParse<Brightness>(value, out var brightness)
            ? brightness : throw new FormatException("Unknown LED brightness.")).ConfigureAwait(false);
    }

    public void SetBrightness(Brightness brightness) => RunSync(() => SetBrightnessAsync(brightness));

    public async Task SetBrightnessAsync(Brightness brightness)
    {
        ValidateEnum(brightness, nameof(brightness));
        var response = await RequestArrayAsync("set_led_b", brightness).ConfigureAwait(false);
        CheckMessage(response, "Unable to set brightness of air humidifier");
    }

    public int GetTargetHumidity() => RunSync(() => GetTargetHumidityAsync());

    public async Task<int> GetTargetHumidityAsync()
    {
        return await ReadPropertyValueAsync("limit_hum", value => int.Parse(value, CultureInfo.InvariantCulture)).ConfigureAwait(false);
    }

    public bool GetBuzzerEnabled() => RunSync(() => GetBuzzerEnabledAsync());

    public async Task<bool> GetBuzzerEnabledAsync()
    {
        return await ReadPropertyValueAsync("buzzer", MiioResponseReader.OnOff).ConfigureAwait(false);
    }

    /// <summary>Sets whether buzzer is enabled on the device.</summary>
    public void SetBuzzerEnabled(bool enabled) => RunSync(() => SetBuzzerEnabledAsync(enabled));

    /// <inheritdoc cref="SetBuzzerEnabled"/>
    public async Task SetBuzzerEnabledAsync(bool enabled)
    {
        var response = await RequestArrayAsync("set_buzzer", enabled ? "on" : "off").ConfigureAwait(false);
        CheckMessage(response, "Unable to set buzzer on air humidifier");
    }

    public bool GetChildLockEnabled() => RunSync(() => GetChildLockEnabledAsync());

    public async Task<bool> GetChildLockEnabledAsync()
    {
        return await ReadPropertyValueAsync("child_lock", MiioResponseReader.OnOff).ConfigureAwait(false);
    }

    /// <summary>Sets whether child lock is enabled on the device.</summary>
    public void SetChildLockEnabled(bool enabled) => RunSync(() => SetChildLockEnabledAsync(enabled));

    /// <inheritdoc cref="SetChildLockEnabled"/>
    public async Task SetChildLockEnabledAsync(bool enabled)
    {
        var response = await RequestArrayAsync("set_child_lock", enabled ? "on" : "off").ConfigureAwait(false);
        CheckMessage(response, "Unable to set child lock on air humidifier");
    }
}
