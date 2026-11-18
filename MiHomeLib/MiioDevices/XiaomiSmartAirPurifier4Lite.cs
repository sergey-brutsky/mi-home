using MiHomeLib.MiioDevices.Internal;
// MIoT specification https://home.miot-spec.com/spec/zhimi.airp.rma3
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices;

public enum FaultState { NoFaults = 0, Motor = 2, Pm25Sensor = 3 }
public enum OperationMode { Auto = 0, Sleep = 1, Favorite = 2 }
public enum ScreenBrightness { Off = 0, Bright = 1, Brightest = 2 }
public enum AirQuality { Excellent = 0, Good = 1, Moderate = 2, Poor = 3, HeavyPollution = 4, Hazardous = 5 }

public class XiaomiSmartAirPurifier4Lite : MiotGenericDevice
{
    #region Types and snapshots

    /// <summary>Current power, operation, environment and motor readings. PM2.5 is in micrograms per cubic metre, temperature in degrees Celsius.</summary>
    public sealed record DeviceStatus(bool IsTurnedOn, FaultState DeviceFaultState, OperationMode Mode,
        byte Humidity, ushort Pm25Density, float Temperature, AirQuality AirQuality, ushort MotorSpeedRpm);

    /// <summary>Current operation mode, sound, display, child lock and favorite-mode fan level.</summary>
    public sealed record DeviceSettings(OperationMode Mode, bool BuzzerEnabled, ScreenBrightness ScreenBrightness,
        bool ChildLockEnabled, byte FanLevel);

    /// <summary>Remaining filter life in percent, used time in hours and remaining time in days.</summary>
    public sealed record FilterInfo(byte LifePercent, ushort UsedHours, ushort RemainingDays);

    /// <summary>Raw device-information service values. Firmware may return placeholders such as "hello". DeviceId is property 1/3, SerialNumber is property 1/5.</summary>
    public sealed record DeviceInformation(string Manufacturer, string Model, string DeviceId,
        string FirmwareRevision, string SerialNumber);

    #endregion

    #region Constants and construction

    public const string MARKET_MODEL = "AC-M17-SC";
    public const string MODEL = "zhimi.airp.rma3";

    public XiaomiSmartAirPurifier4Lite(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiSmartAirPurifier4Lite(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal) {}

    #endregion

    #region Reading snapshots

    /// <summary>Reads eight current power, mode, environment and motor properties in one request.</summary>
    public DeviceStatus GetStatus() => RunSync(GetStatusAsync);

    /// <inheritdoc cref="GetStatus"/>
    public async Task<DeviceStatus> GetStatusAsync()
    {
        var values = await GetMiotPropertiesAsync([(2, 1), (2, 2), (2, 4), (3, 1),
            (3, 4), (3, 7), (3, 8), (9, 1)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new DeviceStatus(bool.Parse(values[0]),
            (FaultState)byte.Parse(values[1], CultureInfo.InvariantCulture),
            (OperationMode)byte.Parse(values[2], CultureInfo.InvariantCulture),
            byte.Parse(values[3], CultureInfo.InvariantCulture), ushort.Parse(values[4], CultureInfo.InvariantCulture),
            float.Parse(values[5], CultureInfo.InvariantCulture), (AirQuality)byte.Parse(values[6], CultureInfo.InvariantCulture),
            ushort.Parse(values[7], CultureInfo.InvariantCulture)), "Unable to read Air Purifier 4 Lite status");
    }

    /// <summary>Reads operation mode, sound, display, child lock and favorite-mode fan level in one request.</summary>
    public DeviceSettings GetSettings() => RunSync(GetSettingsAsync);

    /// <inheritdoc cref="GetSettings"/>
    public async Task<DeviceSettings> GetSettingsAsync()
    {
        var values = await GetMiotPropertiesAsync([(2, 4), (6, 1), (7, 2), (8, 1), (11, 1)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new DeviceSettings(
            (OperationMode)byte.Parse(values[0], CultureInfo.InvariantCulture), bool.Parse(values[1]),
            (ScreenBrightness)byte.Parse(values[2], CultureInfo.InvariantCulture), bool.Parse(values[3]),
            byte.Parse(values[4], CultureInfo.InvariantCulture)), "Unable to read Air Purifier 4 Lite settings");
    }

    /// <summary>Reads filter life, used hours and remaining days in one request.</summary>
    public FilterInfo GetFilterInfo() => RunSync(GetFilterInfoAsync);

    /// <inheritdoc cref="GetFilterInfo"/>
    public async Task<FilterInfo> GetFilterInfoAsync()
    {
        var values = await GetMiotPropertiesAsync([(4, 1), (4, 3), (4, 4)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new FilterInfo(byte.Parse(values[0], CultureInfo.InvariantCulture),
            ushort.Parse(values[1], CultureInfo.InvariantCulture), ushort.Parse(values[2], CultureInfo.InvariantCulture)),
            "Unable to read Air Purifier 4 Lite filter info");
    }

    /// <summary>Reads raw manufacturer, model, device ID, firmware revision and serial number from MIoT service 1. Firmware may return placeholder strings.</summary>
    public DeviceInformation GetDeviceInformation() => RunSync(GetDeviceInformationAsync);

    /// <inheritdoc cref="GetDeviceInformation"/>
    public async Task<DeviceInformation> GetDeviceInformationAsync()
    {
        var values = await GetMiotPropertiesAsync([(1, 1), (1, 2), (1, 3), (1, 4), (1, 5)]).ConfigureAwait(false);
        return new DeviceInformation(values[0], values[1], values[2], values[3], values[4]);
    }

    #endregion

    #region Power and operation mode

    /// <summary>
    /// Get power state on/off
    /// </summary>
    public bool GetPowerState() => RunSync(GetPowerStateAsync);

    /// <inheritdoc cref="GetPowerState"/>
    public async Task<bool> GetPowerStateAsync() => await ReadMiotValueAsync(2, 1, bool.Parse).ConfigureAwait(false);

    /// <summary>
    /// Turn on the air purifier
    /// </summary>
    public void TurnOn() => RunSync(() => TurnOnAsync());

    /// <inheritdoc cref="TurnOn"/>
    public async Task TurnOnAsync() => await SetMiotPropertyAsync(2, 1, true).ConfigureAwait(false);

    /// <summary>
    /// Turn off the air purifier
    /// </summary>
    public void TurnOff() => RunSync(() => TurnOffAsync());

    /// <inheritdoc cref="TurnOff"/>
    public async Task TurnOffAsync() => await SetMiotPropertyAsync(2, 1, false).ConfigureAwait(false);

    /// <summary>
    /// Toggle power on the air purifier
    /// </summary>
    public void TogglePower() => RunSync(() => TogglePowerAsync());

    /// <inheritdoc cref="TogglePower"/>
    public async Task TogglePowerAsync() => await CallMiotActionAsync(2, 1, []).ConfigureAwait(false);

    /// <summary>
    /// Get device fault state
    /// </summary>
    public FaultState GetDeviceFaultState() => RunSync(GetDeviceFaultStateAsync);

    /// <inheritdoc cref="GetDeviceFaultState"/>
    public async Task<FaultState> GetDeviceFaultStateAsync() => (FaultState)await ReadMiotValueAsync(2, 2, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get/Set operation mode: Auto, Sleep, Favorite
    /// </summary>
    public OperationMode GetMode() => RunSync(GetModeAsync);

    /// <summary>Sets Mode on the device.</summary>
    public void SetMode(OperationMode value) => RunSync(() => SetModeAsync(value));

    /// <inheritdoc cref="GetMode"/>
    public async Task<OperationMode> GetModeAsync() => (OperationMode)await ReadMiotValueAsync(2, 4, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetMode"/>
    public async Task SetModeAsync(OperationMode value)
    {
        await SetMiotPropertyAsync(2, 4, value).ConfigureAwait(false);
    }

    #endregion

    #region Environmental measurements

    /// <summary>
    /// Get relative humidity percentage
    /// </summary>
    public byte GetHumidity() => RunSync(GetHumidityAsync);

    /// <inheritdoc cref="GetHumidity"/>
    public async Task<byte> GetHumidityAsync() => await ReadMiotValueAsync(3, 1, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get PM2.5 density in µg/m³
    /// </summary>
    public ushort GetPm25Density() => RunSync(GetPm25DensityAsync);

    /// <inheritdoc cref="GetPm25Density"/>
    public async Task<ushort> GetPm25DensityAsync() => await ReadMiotValueAsync(3, 4, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get temperature in degrees Celsius
    /// </summary>
    public float GetTemperature() => RunSync(GetTemperatureAsync);

    /// <inheritdoc cref="GetTemperature"/>
    public async Task<float> GetTemperatureAsync() => await ReadMiotValueAsync(3, 7, value => float.Parse(value, CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get air quality level
    /// </summary>
    public AirQuality GetAirQuality() => RunSync(GetAirQualityAsync);

    /// <inheritdoc cref="GetAirQuality"/>
    public async Task<AirQuality> GetAirQualityAsync() => (AirQuality)await ReadMiotValueAsync(3, 8, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    #endregion

    #region Filter

    /// <summary>
    /// Get filter life level percentage
    /// </summary>
    public byte GetFilterLifeLevel() => RunSync(GetFilterLifeLevelAsync);

    /// <inheritdoc cref="GetFilterLifeLevel"/>
    public async Task<byte> GetFilterLifeLevelAsync() => await ReadMiotValueAsync(4, 1, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get filter used time in hours
    /// </summary>
    public ushort GetFilterUsedTime() => RunSync(GetFilterUsedTimeAsync);

    /// <inheritdoc cref="GetFilterUsedTime"/>
    public async Task<ushort> GetFilterUsedTimeAsync() => await ReadMiotValueAsync(4, 3, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get filter left time in days
    /// </summary>
    public ushort GetFilterLeftTime() => RunSync(GetFilterLeftTimeAsync);

    /// <inheritdoc cref="GetFilterLeftTime"/>
    public async Task<ushort> GetFilterLeftTimeAsync() => await ReadMiotValueAsync(4, 4, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Reset filter life
    /// </summary>
    public void ResetFilterLife() => RunSync(() => ResetFilterLifeAsync());

    /// <inheritdoc cref="ResetFilterLife"/>
    public async Task ResetFilterLifeAsync() => await CallMiotActionAsync(4, 1, []).ConfigureAwait(false);

    /// <summary>Firmware debug counter for filter usage, in hours. Writing changes the service value and requires hardware verification.</summary>
    public ushort GetFilterUsedTimeDebug() => RunSync(GetFilterUsedTimeDebugAsync);

    /// <summary>Sets FilterUsedTimeDebug on the device.</summary>
    public void SetFilterUsedTimeDebug(ushort value) => RunSync(() => SetFilterUsedTimeDebugAsync(value));

    /// <summary>Reads the firmware's debug filter-use counter (9/2), in hours.</summary>
    public async Task<ushort> GetFilterUsedTimeDebugAsync() =>
        await ReadMiotValueAsync(9, 2, value => ushort.Parse(value, CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>Writes the firmware's debug filter-use counter (9/2), from 0 to 3500 hours. Requires hardware verification.</summary>
    public async Task SetFilterUsedTimeDebugAsync(ushort value)
    {
        if (value > 3500) throw new ArgumentOutOfRangeException(nameof(value), value, "Expected 0 to 3500 hours.");
        await SetMiotPropertyAsync(9, 2, value).ConfigureAwait(false);
    }

    #endregion

    #region Sound, display and child lock

    /// <summary>
    /// Enable/disable buzzer
    /// </summary>
    public bool GetBuzzerEnabled() => RunSync(GetBuzzerEnabledAsync);

    /// <summary>Sets BuzzerEnabled on the device.</summary>
    public void SetBuzzerEnabled(bool value) => RunSync(() => SetBuzzerEnabledAsync(value));

    /// <inheritdoc cref="GetBuzzerEnabled"/>
    public async Task<bool> GetBuzzerEnabledAsync() => await ReadMiotValueAsync(6, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetBuzzerEnabled"/>
    public async Task SetBuzzerEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(6, 1, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Get/Set screen brightness: Off, Bright, Brightest
    /// </summary>
    public ScreenBrightness GetScreenBrightness() => RunSync(GetScreenBrightnessAsync);

    /// <summary>Sets ScreenBrightness on the device.</summary>
    public void SetScreenBrightness(ScreenBrightness value) => RunSync(() => SetScreenBrightnessAsync(value));

    /// <inheritdoc cref="GetScreenBrightness"/>
    public async Task<ScreenBrightness> GetScreenBrightnessAsync() => (ScreenBrightness)await ReadMiotValueAsync(7, 2, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetScreenBrightness"/>
    public async Task SetScreenBrightnessAsync(ScreenBrightness value)
    {
        await SetMiotPropertyAsync(7, 2, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Enable/disable child lock
    /// </summary>
    public bool GetChildLockEnabled() => RunSync(GetChildLockEnabledAsync);

    /// <summary>Sets ChildLockEnabled on the device.</summary>
    public void SetChildLockEnabled(bool value) => RunSync(() => SetChildLockEnabledAsync(value));

    /// <inheritdoc cref="GetChildLockEnabled"/>
    public async Task<bool> GetChildLockEnabledAsync() => await ReadMiotValueAsync(8, 1, bool.Parse).ConfigureAwait(false);

    /// <inheritdoc cref="SetChildLockEnabled"/>
    public async Task SetChildLockEnabledAsync(bool value)
    {
        await SetMiotPropertyAsync(8, 1, value).ConfigureAwait(false);
    }

    #endregion

    #region Fan and AQI service settings

    /// <summary>
    /// Get motor speed in RPM
    /// </summary>
    public ushort GetMotorSpeedRpm() => RunSync(GetMotorSpeedRpmAsync);

    /// <inheritdoc cref="GetMotorSpeedRpm"/>
    public async Task<ushort> GetMotorSpeedRpmAsync() => await ReadMiotValueAsync(9, 1, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get/Set the favorite-mode fan level, range [0, 14]. Select Favorite mode separately to use it.
    /// </summary>
    public byte GetFanLevel() => RunSync(GetFanLevelAsync);

    /// <summary>Sets FanLevel on the device.</summary>
    public void SetFanLevel(byte value) => RunSync(() => SetFanLevelAsync(value));

    /// <inheritdoc cref="GetFanLevel"/>
    public async Task<byte> GetFanLevelAsync() => await ReadMiotValueAsync(11, 1, value => byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <inheritdoc cref="SetFanLevel"/>
    public async Task SetFanLevelAsync(byte value)
    {
        if (value > 14) throw new ArgumentOutOfRangeException(nameof(value), value, "Fan level should be in range 0 - 14");
        await SetMiotPropertyAsync(11, 1, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Toggle operation mode
    /// </summary>
    public void ToggleMode() => RunSync(() => ToggleModeAsync());

    /// <inheritdoc cref="ToggleMode"/>
    public async Task ToggleModeAsync() => await CallMiotActionAsync(9, 1, []).ConfigureAwait(false);

    /// <summary>Firmware AQI update heartbeat (10/1), from 0 to 65535. Units and the effect of zero are undocumented.</summary>
    public ushort GetAqiUpdateHeartbeat() => RunSync(GetAqiUpdateHeartbeatAsync);

    /// <summary>Sets AqiUpdateHeartbeat on the device.</summary>
    public void SetAqiUpdateHeartbeat(ushort value) => RunSync(() => SetAqiUpdateHeartbeatAsync(value));

    /// <summary>Reads the firmware-native AQI update heartbeat value.</summary>
    public async Task<ushort> GetAqiUpdateHeartbeatAsync() =>
        await ReadMiotValueAsync(10, 1, value => ushort.Parse(value, CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>Writes the firmware-native AQI update heartbeat value. Its effect requires hardware verification.</summary>
    public async Task SetAqiUpdateHeartbeatAsync(ushort value) =>
        await SetMiotPropertyAsync(10, 1, value).ConfigureAwait(false);

    #endregion

    public override string ToString() => GetDeviceInfo(MARKET_MODEL, MODEL);
}
