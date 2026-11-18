using MiHomeLib.MiioDevices.Internal;
using System.Threading.Tasks;
// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/cuco.plug.v2eur
// Your contributions are appreciated
using System;

namespace MiHomeLib.MiioDevices;

public class XiaomiSmartPlug2Euro : MiotGenericDevice
{
    public const string MARKET_MODEL = "ZNCZ302KK";
    public const string MODEL = "cuco.plug.v2eur";
    public enum PowerOnState
    {
        RestoreLast = 0,
        TurnOnPower = 1,
        TurnOffPower = 2
    }
    public enum CycleState
    {
        TurnOff = 0,
        TurnOn = 1,
        RestoreLast = 2
    }
    public enum FaultStateValue
    {
        NoFaults = 0,
        OverTemperature = 1,
        Overloaded = 2
    } 
    public XiaomiSmartPlug2Euro(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiSmartPlug2Euro(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal) {}

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
    /// Toggle power on the plug
    /// </summary>
    public void TogglePower() => RunSync(() => TogglePowerAsync());

    /// <inheritdoc cref="TogglePower"/>
    public async Task TogglePowerAsync() => await CallMiotActionAsync(2, 1, []).ConfigureAwait(false);

    /// <summary>
    /// Enter the state after electricity outage: restore last state, turn on, turn off    
    /// </summary>
    public PowerOnState GetDefaultPowerOnState() => RunSync(GetDefaultPowerOnStateAsync);

    /// <summary>Sets DefaultPowerOnState on the device.</summary>
    public void SetDefaultPowerOnState(PowerOnState value) => RunSync(() => SetDefaultPowerOnStateAsync(value));

    /// <inheritdoc cref="GetDefaultPowerOnState"/>
    public async Task<PowerOnState> GetDefaultPowerOnStateAsync() => (PowerOnState)await ReadMiotValueAsync(2, 2, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetDefaultPowerOnState"/>
    public async Task SetDefaultPowerOnStateAsync(PowerOnState value)
    {
        await SetMiotPropertyAsync(2, 2, value).ConfigureAwait(false);
    }


    /// <summary>
    /// Get fault state of the plug
    /// </summary>
    public FaultStateValue GetDeviceFaultState() => RunSync(GetDeviceFaultStateAsync);

    /// <inheritdoc cref="GetDeviceFaultState"/>
    public async Task<FaultStateValue> GetDeviceFaultStateAsync() => (FaultStateValue)await ReadMiotValueAsync(2, 3, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Enable/disable charging protection functionality
    /// </summary>
    public bool GetChargingProtectionEnabled() => RunSync(GetChargingProtectionEnabledAsync);

    /// <summary>Sets ChargingProtectionEnabled on the device.</summary>
    public void SetChargingProtectionEnabled(bool value) => RunSync(() => SetChargingProtectionEnabledAsync(value));

    /// <inheritdoc cref="GetChargingProtectionEnabled"/>
    public async Task<bool> GetChargingProtectionEnabledAsync() => await ReadMiotValueAsync(4, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetChargingProtectionEnabled"/>
    public async Task SetChargingProtectionEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(4, 1, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Charging protection power threshold in watt
    /// If power is less than this threshold (for specific amount of time) plug will turn off automatically
    /// </summary>
    public ushort GetChargingProtectionPowerThreshold() => RunSync(GetChargingProtectionPowerThresholdAsync);

    /// <summary>Sets ChargingProtectionPowerThreshold on the device.</summary>
    public void SetChargingProtectionPowerThreshold(ushort value) => RunSync(() => SetChargingProtectionPowerThresholdAsync(value));

    /// <inheritdoc cref="GetChargingProtectionPowerThreshold"/>
    public async Task<ushort> GetChargingProtectionPowerThresholdAsync() => await ReadMiotValueAsync(4, 2, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetChargingProtectionPowerThreshold"/>
    public async Task SetChargingProtectionPowerThresholdAsync(ushort value)
    {
        if(value < 2 || value > 1200) throw new ArgumentOutOfRangeException(nameof(value), value, "Threshold should be in range 2 - 1200");

        await SetMiotPropertyAsync(4, 2, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Charging protection time in minutes.
    /// If power is less than specific threshold for this amount of minutes the plug will turn off automatically
    /// </summary>
    public ushort GetChargingProtectionTime() => RunSync(GetChargingProtectionTimeAsync);

    /// <summary>Sets ChargingProtectionTime on the device.</summary>
    public void SetChargingProtectionTime(ushort value) => RunSync(() => SetChargingProtectionTimeAsync(value));

    /// <inheritdoc cref="GetChargingProtectionTime"/>
    public async Task<ushort> GetChargingProtectionTimeAsync() => await ReadMiotValueAsync(4, 3, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetChargingProtectionTime"/>
    public async Task SetChargingProtectionTimeAsync(ushort value)
    {
        if(value < 1 || value > 300) throw new ArgumentOutOfRangeException(nameof(value), value, "Time should be in range 1 - 300 minutes");
        await SetMiotPropertyAsync(4, 3, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Enable/disable cycle
    /// </summary>
    public bool GetCyclingEnabled() => RunSync(GetCyclingEnabledAsync);

    /// <summary>Sets CyclingEnabled on the device.</summary>
    public void SetCyclingEnabled(bool value) => RunSync(() => SetCyclingEnabledAsync(value));

    /// <inheritdoc cref="GetCyclingEnabled"/>
    public async Task<bool> GetCyclingEnabledAsync() => await ReadMiotValueAsync(5, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetCyclingEnabled"/>
    public async Task SetCyclingEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(5, 1, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Enable/disable cycling
    /// This plug can turn on/off specific amount of times.<br/>
    /// minutesOn - amount of minutes in "turn on" state in range 0 - 1439.<br/>
    /// minutesOff - amount of minutes in "turn off" state in range 0 - 1439.<br/>
    /// cycleState - what state should be in the end of cycle: turn off, turn on, restore last<br/>
    /// cyclesCount - amount of cycles in range 0 - 10 (0 - unlimited)
    /// </summary>    
    public (ushort minutesOn, ushort minutesOff, CycleState cycleState, byte cyclesCount) GetCycleData() => RunSync(GetCycleDataAsync);

    /// <summary>Sets CycleData on the device.</summary>
    public void SetCycleData((ushort minutesOn, ushort minutesOff, CycleState cycleState, byte cyclesCount) value) => RunSync(() => SetCycleDataAsync(value));

    /// <inheritdoc cref="GetCycleData"/>
    public async Task<(ushort minutesOn, ushort minutesOff, CycleState cycleState, byte cyclesCount)> GetCycleDataAsync()
    {
        var chunks = (await GetMiotPropertyAsync(5, 2).ConfigureAwait(false)).Split(';');
        const string operation = "Unable to read cycle data (5/2)";
        if (chunks.Length != 4) throw new InvalidOperationException($"{operation}: expected four semicolon-separated values.");
        return MiioResponseReader.ConvertValue(() => (
            ushort.Parse(chunks[0], System.Globalization.CultureInfo.InvariantCulture),
            ushort.Parse(chunks[1], System.Globalization.CultureInfo.InvariantCulture),
            (CycleState)int.Parse(chunks[2], System.Globalization.CultureInfo.InvariantCulture),
            byte.Parse(chunks[3], System.Globalization.CultureInfo.InvariantCulture)), operation);
    }

    /// <inheritdoc cref="SetCycleData"/>
    public async Task SetCycleDataAsync((ushort minutesOn, ushort minutesOff, CycleState cycleState, byte cyclesCount) value)
    {
        ValidateEnum(value.cycleState, nameof(value));
        if (value.minutesOn < 1 || value.minutesOn > 1439) throw new ArgumentOutOfRangeException(nameof(value), value, "Minutes on should be in range 1 - 1439");
        if (value.minutesOff < 1 || value.minutesOff > 1439) throw new ArgumentOutOfRangeException(nameof(value), value, "Minutes off should be in range 1 - 1439");
        if (value.cyclesCount > 10) throw new ArgumentOutOfRangeException(nameof(value), value, "Cycle count should be in range 0 - 10");

        var str = $"{value.minutesOn};{value.minutesOff};{(byte)value.cycleState};{value.cyclesCount}";
        await SetMiotPropertyAsync(5, 2, string.Empty, str).ConfigureAwait(false);
    }

    /// <summary>
    /// Enable/disable lock of physical button on the plug
    /// </summary>
    public bool GetPhysicalControlBlocked() => RunSync(GetPhysicalControlBlockedAsync);

    /// <summary>Sets PhysicalControlBlocked on the device.</summary>
    public void SetPhysicalControlBlocked(bool value) => RunSync(() => SetPhysicalControlBlockedAsync(value));

    /// <inheritdoc cref="GetPhysicalControlBlocked"/>
    public async Task<bool> GetPhysicalControlBlockedAsync() => await ReadMiotValueAsync(7, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetPhysicalControlBlocked"/>
    public async Task SetPhysicalControlBlockedAsync(bool value)
    {
        await SetMiotPropertyAsync(7, 1, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Enable/disable max power limit for the plug
    /// After reaching this limit plug will turn off
    /// </summary>
    public bool GetMaxPowerLimitEnabled() => RunSync(GetMaxPowerLimitEnabledAsync);

    /// <summary>Sets MaxPowerLimitEnabled on the device.</summary>
    public void SetMaxPowerLimitEnabled(bool value) => RunSync(() => SetMaxPowerLimitEnabledAsync(value));

    /// <inheritdoc cref="GetMaxPowerLimitEnabled"/>
    public async Task<bool> GetMaxPowerLimitEnabledAsync() => await ReadMiotValueAsync(9, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetMaxPowerLimitEnabled"/>
    public async Task SetMaxPowerLimitEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(9, 1, value).ConfigureAwait(false);
    }

     /// <summary>
    /// Max power threshold in watts, range 300 - 3600, step 100
    /// Works only if max power limit is enabled
    /// </summary>
    public ushort GetMaxPowerThreshold() => RunSync(GetMaxPowerThresholdAsync);

    /// <summary>Sets MaxPowerThreshold on the device.</summary>
    public void SetMaxPowerThreshold(ushort value) => RunSync(() => SetMaxPowerThresholdAsync(value));

    /// <inheritdoc cref="GetMaxPowerThreshold"/>
    public async Task<ushort> GetMaxPowerThresholdAsync() => await ReadMiotValueAsync(9, 2, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetMaxPowerThreshold"/>
    public async Task SetMaxPowerThresholdAsync(ushort value)
    {
        if(value % 100 != 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Threshold must be a multiple of 100");
        if(value < 300 || value > 3600) throw new ArgumentOutOfRangeException(nameof(value), value, "Threshold should be in range 300 - 3600");

        await SetMiotPropertyAsync(9, 2, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Get current power in watts, range 0 - 5000
    /// </summary>
    public ushort GetCurrentElectricPower() => RunSync(GetCurrentElectricPowerAsync);

    /// <inheritdoc cref="GetCurrentElectricPower"/>
    public async Task<ushort> GetCurrentElectricPowerAsync() => await ReadMiotValueAsync(11, 2, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Enable/disable led indicator of the plug
    /// </summary>
    public bool GetLedEnabled() => RunSync(GetLedEnabledAsync);

    /// <summary>Sets LedEnabled on the device.</summary>
    public void SetLedEnabled(bool value) => RunSync(() => SetLedEnabledAsync(value));

    /// <inheritdoc cref="GetLedEnabled"/>
    public async Task<bool> GetLedEnabledAsync() => await ReadMiotValueAsync(13, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetLedEnabled"/>
    public async Task SetLedEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(13, 1, value).ConfigureAwait(false);
    }

    //WARNING: Delay functions not implemented according to spec !
    // Because on this version of firmware 1.0.6@ESP32C3
    // They don't work :(
    public override string ToString() => GetDeviceInfo(MARKET_MODEL, MODEL);
}

