using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.Helpers;
using MiHomeLib.MqttGateway.Discovery;
// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mgl03
// Your contributions are appreciated
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.MqttGateway;

/// <summary>
/// Xiaomi Multimode Gateway (CN) ZNDMWG03LM
/// </summary>    
public class MultimodeGateway : MqttGatewayBase
{
    public const string MARKET_MODEL = "ZNDMWG03LM";
    public const string MODEL = "lumi.gateway.mgl03";
    private readonly Dictionary<ArmingModeValue, ushort> _delayTimeMap = new()
    {
        { ArmingModeValue.Basic, 9 },
        { ArmingModeValue.Home, 12 },
        { ArmingModeValue.Away, 15 },
        { ArmingModeValue.Sleep, 18 },
    };

    private readonly Dictionary<ArmingModeValue, ushort> _alarmDurationMap = new()
    {
        { ArmingModeValue.Basic, 10 },
        { ArmingModeValue.Home, 13 },
        { ArmingModeValue.Away, 16 },
        { ArmingModeValue.Sleep, 19 },
    };

    private readonly Dictionary<ArmingModeValue, ushort> _alarmVolumeMap = new()
    {
        { ArmingModeValue.Basic, 11 },
        { ArmingModeValue.Home, 14 },
        { ArmingModeValue.Away, 17 },
        { ArmingModeValue.Sleep, 20 },
    };

    public enum ArmingModeValue
    {
        Basic = 0,
        Home = 1,
        Away = 2,
        Sleep = 3,
    }

    public enum AlarmStateValue
    {
        NoAlarm = 0,
        SecurityAlarm = 1,
        NonSecurityAlarm = 2,
    }

    public enum VolumeLevelValue
    {
        Silent = 0,
        Low = 1,
        Middle = 2,
        High = 3,
    }

    public MultimodeGateway(string ip, string token, string did = "", int port = 1883) : this(ip, token, did, port, null) { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by MiHomeLogging.SetLoggerFactory</param>
    /// <param name="did">Gateway device identifier.</param>
    /// <param name="ip">Gateway IP address.</param>
    /// <param name="port">Gateway service port.</param>
    /// <param name="token">Gateway miIO token.</param>
    public MultimodeGateway(string ip, string token, string did, int port, ILoggerFactory loggerFactory) :
        this(
                did,
                ResourceFactory.CreatePair<IMiioTransport, IMqttTransport>(
                    () => new MiioTransport(ip, token),
                    () => new MqttTransport(ip, port, [.. _zigbeeTopics, .. _bleTopics], _zigbeeCommandsTopic, ResolveLoggerFactory(loggerFactory))),
                new CommonDevicesDiscoverer(ip, 23, "admin", "# ", "/data/zigbee/device.info", "/data/miio/mible_local.db"),
                loggerFactory
        )
    { }

    private MultimodeGateway(string did, (IMiioTransport First, IMqttTransport Second) transports,
        IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory)
        : this(did, transports.First, transports.Second, devicesDiscoverer, loggerFactory) { }

    internal MultimodeGateway(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory = null)
        : base(did, miioTransport, mqttTransport, devicesDiscoverer, loggerFactory)
    {
    }

    /// <summary>
    /// Enable/disable led indicator light
    /// </summary>
    public bool GetLedEnabled() => RunSync(GetLedEnabledAsync);

    /// <inheritdoc cref="GetLedEnabled"/>
    public async Task<bool> GetLedEnabledAsync()
    {
        return int.Parse(await GetMiotPropertyAsync(6, 6, _did).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>Sets LedEnabled on the gateway.</summary>
    public void SetLedEnabled(bool value) => RunSync(() => SetLedEnabledAsync(value));

    /// <inheritdoc cref="SetLedEnabled"/>
    public Task SetLedEnabledAsync(bool value)
    {
        return SetMiotPropertyAsync(6, 6, _did, value ? 1 : 0);
    }

    /// <summary>
    /// Gateway arming mode: basic, home, away, sleep
    /// </summary>
    public ArmingModeValue GetArmingMode() => RunSync(GetArmingModeAsync);

    /// <inheritdoc cref="GetArmingMode"/>
    public async Task<ArmingModeValue> GetArmingModeAsync()
    {
        return (ArmingModeValue)ushort.Parse(await GetMiotPropertyAsync(3, 1, _did).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>Sets ArmingMode on the gateway.</summary>
    public void SetArmingMode(ArmingModeValue value) => RunSync(() => SetArmingModeAsync(value));

    /// <inheritdoc cref="SetArmingMode"/>
    public Task SetArmingModeAsync(ArmingModeValue value)
    {
        return SetMiotPropertyAsync(3, 1, _did, value);
    }

    /// <summary>
    /// Gateway alarm state: no alarm, security alarm, non-security alarms
    /// </summary>
    public AlarmStateValue GetAlarmState() => RunSync(GetAlarmStateAsync);

    /// <inheritdoc cref="GetAlarmState"/>
    public async Task<AlarmStateValue> GetAlarmStateAsync()
    {
        return (AlarmStateValue)byte.Parse(await GetMiotPropertyAsync(3, 22, _did).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>Sets AlarmState on the gateway.</summary>
    public void SetAlarmState(AlarmStateValue value) => RunSync(() => SetAlarmStateAsync(value));

    /// <inheritdoc cref="SetAlarmState"/>
    public Task SetAlarmStateAsync(AlarmStateValue value)
    {
        return SetMiotPropertyAsync(3, 22, _did, value);
    }

    /// <summary>
    /// Get delay time before arming mode becomes effective
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <returns>delay time in seconds (range 0-60)</returns>
    public byte GetDelayTimeForArmingMode(ArmingModeValue armingMode) => RunSync(() => GetDelayTimeForArmingModeAsync(armingMode));

    /// <inheritdoc cref="GetDelayTimeForArmingMode"/>
    public async Task<byte> GetDelayTimeForArmingModeAsync(ArmingModeValue armingMode)
    {
        return byte.Parse(await GetMiotPropertyAsync(3, _delayTimeMap[armingMode], _did).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Set delay time for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <param name="delayTime">seconds in range 0 - 60</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetDelayTimeForArmingMode(ArmingModeValue armingMode, byte delayTime) =>
        RunSync(() => SetDelayTimeForArmingModeAsync(armingMode, delayTime));

    /// <inheritdoc cref="SetDelayTimeForArmingMode"/>
    public Task SetDelayTimeForArmingModeAsync(ArmingModeValue armingMode, byte delayTime)
    {
        if (delayTime > 60) throw new ArgumentOutOfRangeException(nameof(delayTime), delayTime, "Delay time should be in range 0 - 60");

        return SetMiotPropertyAsync(3, _delayTimeMap[armingMode], _did, delayTime);
    }

    /// <summary>
    /// Get alarm duration time for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <returns>duration time in seconds (range 0-2147483647)</returns>
    public uint GetAlarmDurationForArmingMode(ArmingModeValue armingMode) => RunSync(() => GetAlarmDurationForArmingModeAsync(armingMode));

    /// <inheritdoc cref="GetAlarmDurationForArmingMode"/>
    public async Task<uint> GetAlarmDurationForArmingModeAsync(ArmingModeValue armingMode) =>
        uint.Parse(await GetMiotPropertyAsync(3, _alarmDurationMap[armingMode], _did).ConfigureAwait(false), CultureInfo.InvariantCulture);

    /// <summary>
    /// Set delay time for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <param name="durationTime">seconds in range 0 - 2147483647</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetAlarmDurationForArmingMode(ArmingModeValue armingMode, uint durationTime) =>
        RunSync(() => SetAlarmDurationForArmingModeAsync(armingMode, durationTime));

    /// <inheritdoc cref="SetAlarmDurationForArmingMode"/>
    public Task SetAlarmDurationForArmingModeAsync(ArmingModeValue armingMode, uint durationTime)
    {
        if (durationTime > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(durationTime), durationTime, "Duration time should be in range 0 - 2147483647");

        return SetMiotPropertyAsync(3, _alarmDurationMap[armingMode], _did, durationTime);
    }

    /// <summary>
    /// Get alarm volume for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <returns>Silent, Low, Middle, High</returns>
    public VolumeLevelValue GetAlarmVolumeForArmingMode(ArmingModeValue armingMode) => RunSync(() => GetAlarmVolumeForArmingModeAsync(armingMode));

    /// <inheritdoc cref="GetAlarmVolumeForArmingMode"/>
    public async Task<VolumeLevelValue> GetAlarmVolumeForArmingModeAsync(ArmingModeValue armingMode) =>
        (VolumeLevelValue)byte.Parse(await GetMiotPropertyAsync(3, _alarmVolumeMap[armingMode], _did).ConfigureAwait(false), CultureInfo.InvariantCulture);

    /// <summary>
    /// Set alarm volume for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <param name="volumeLevel">Silent, Low, Middle, High</param>
    public void SetAlarmVolumeForArmingMode(ArmingModeValue armingMode, VolumeLevelValue volumeLevel) =>
        RunSync(() => SetAlarmVolumeForArmingModeAsync(armingMode, volumeLevel));

    /// <inheritdoc cref="SetAlarmVolumeForArmingMode"/>
    public Task SetAlarmVolumeForArmingModeAsync(ArmingModeValue armingMode, VolumeLevelValue volumeLevel) =>
        SetMiotPropertyAsync(3, _alarmVolumeMap[armingMode], _did, volumeLevel);

    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
