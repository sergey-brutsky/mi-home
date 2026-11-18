using MiHomeLib.MiioDevices.Internal;
using System.Threading.Tasks;
// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/chuangmi.plug.212a01
// Your contributions are appreciated
using System;

namespace MiHomeLib.MiioDevices;

public class MijiaSmartSocket2China : MiotGenericDevice
{
    public const string MARKET_MODEL = "ZNCZ07CM";
    public const string MODEL = "chuangmi.plug.212a01";

    public MijiaSmartSocket2China(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal MijiaSmartSocket2China(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal)
    {}

    /// <summary>
    /// Get power state on/off
    /// </summary>
    public bool GetPowerState() => RunSync(GetPowerStateAsync);

    /// <inheritdoc cref="GetPowerState"/>
    public async Task<bool> GetPowerStateAsync() => await ReadMiotValueAsync(2, 1, bool.Parse).ConfigureAwait(false);

    /// <summary>
    /// Turn on power on the plug
    /// </summary>
    public void TurnOn() => RunSync(() => TurnOnAsync());

    /// <inheritdoc cref="TurnOn"/>
    public async Task TurnOnAsync() => await SetMiotPropertyAsync(2, 1, true).ConfigureAwait(false);

    /// <summary>
    /// Turn off power on the plug
    /// </summary>
    public void TurnOff() => RunSync(() => TurnOffAsync());

    /// <inheritdoc cref="TurnOff"/>
    public async Task TurnOffAsync() => await SetMiotPropertyAsync(2, 1, false).ConfigureAwait(false);

    /// <summary>
    /// Toggle power of the plug
    /// </summary>
    public void TogglePower() => RunSync(() => TogglePowerAsync());

    /// <inheritdoc cref="TogglePower"/>
    public async Task TogglePowerAsync() => await SetMiotPropertyOnceAsync(4, 6, true).ConfigureAwait(false);
    
    /// <summary>
    /// Enable/disable led indicator of the plug
    /// </summary>
    public bool GetLedEnabled() => RunSync(GetLedEnabledAsync);

    /// <summary>Sets LedEnabled on the device.</summary>
    public void SetLedEnabled(bool value) => RunSync(() => SetLedEnabledAsync(value));

    /// <inheritdoc cref="GetLedEnabled"/>
    public async Task<bool> GetLedEnabledAsync() => await ReadMiotValueAsync(3, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetLedEnabled"/>
    public async Task SetLedEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(3, 1, value).ConfigureAwait(false);
    }

    // Native timer functions are not implemented, because it's super unclear how they work !
    // If you know how they work, please contribute


    /// <summary>
    /// Get plug temperature in celsius, range 0 - 255
    /// </summary>
    public byte GetTemperature() => RunSync(GetTemperatureAsync);

    /// <inheritdoc cref="GetTemperature"/>
    public async Task<byte> GetTemperatureAsync() => await ReadMiotValueAsync(2, 6, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get electric current in ampere
    /// </summary>
    public ushort GetElectricCurrent() => RunSync(GetElectricCurrentAsync);

    /// <inheritdoc cref="GetElectricCurrent"/>
    public async Task<ushort> GetElectricCurrentAsync() => await ReadMiotValueAsync(5, 2, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get plug voltage in volts
    /// </summary>
    public ushort GetVoltage() => RunSync(GetVoltageAsync);

    /// <inheritdoc cref="GetVoltage"/>
    public async Task<ushort> GetVoltageAsync() => await ReadMiotValueAsync(5, 3, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get plug power in watts
    /// </summary>
    public float GetElectricPower() => RunSync(GetElectricPowerAsync);

    /// <inheritdoc cref="GetElectricPower"/>
    public async Task<float> GetElectricPowerAsync() => await ReadMiotValueAsync(5, 6, value => uint.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false)/100.0f;

    /// <summary>
    /// Enable/disable over power protection
    /// </summary>
    public bool GetOverPowerEnabled() => RunSync(GetOverPowerEnabledAsync);

    /// <summary>Sets OverPowerEnabled on the device.</summary>
    public void SetOverPowerEnabled(bool value) => RunSync(() => SetOverPowerEnabledAsync(value));

    /// <inheritdoc cref="GetOverPowerEnabled"/>
    public async Task<bool> GetOverPowerEnabledAsync() => await ReadMiotValueAsync(7, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetOverPowerEnabled"/>
    public async Task SetOverPowerEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(7, 1, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Get/Set threshold in watts for over power protections
    /// </summary>
    public ushort GetOverPowerThreshold() => RunSync(GetOverPowerThresholdAsync);

    /// <summary>Sets OverPowerThreshold on the device.</summary>
    public void SetOverPowerThreshold(ushort value) => RunSync(() => SetOverPowerThresholdAsync(value));

    /// <inheritdoc cref="GetOverPowerThreshold"/>
    public async Task<ushort> GetOverPowerThresholdAsync() => await ReadMiotValueAsync(7, 2, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetOverPowerThreshold"/>
    public async Task SetOverPowerThresholdAsync(ushort value)
    {
        if (value > 65525) throw new ArgumentOutOfRangeException(nameof(value), value, "Over power threshold should be in range 0 - 65525");
        await SetMiotPropertyAsync(7, 2, value).ConfigureAwait(false);
    }

    public override string ToString() => GetDeviceInfo(MARKET_MODEL, MODEL);
}

