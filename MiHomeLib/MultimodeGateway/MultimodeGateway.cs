// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mgl03
// Your contributions are appreciated
using System;
using System.Collections.Generic;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway;

/// <summary>
/// Xiaomi Multimode Gateway (CN) ZNDMWG03LM
/// </summary>    
public class MultimodeGateway : MultimodeGatewayBase
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

    public enum VolumeLeveValue
    {
        Silen = 0,
        Low = 1,
        Middle = 2,
        High = 3,
    }

    public MultimodeGateway(string ip, string token, string did = "", int port = 1883) : base(ip, token, did, port)
    {
    }

    internal MultimodeGateway(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer) : base(did, miioTransport, mqttTransport, devicesDiscoverer)
    {
    }

    /// <summary>
    /// Enable/disable led indicator light
    /// </summary>
    public bool LedEnabled
    {
        get => int.Parse(GetMiotProperty(6, 6, _did)) == 1;

        set => SetMiotProperty(6, 6, _did, value == true ? 1 : 0);
    }

    /// <summary>
    /// Gateway arming mode: basic, home, away, sleep
    /// </summary>
    public ArmingModeValue ArmingMode
    {
        get => (ArmingModeValue)ushort.Parse(GetMiotProperty(3, 1, _did));
        set => SetMiotProperty(3, 1, _did, value);
    }

    /// <summary>
    /// Gateway alarm state: no alarm, security alarm, non-security alarms
    /// </summary>
    public AlarmStateValue AlarmState
    {
        get => (AlarmStateValue)byte.Parse(GetMiotProperty(3, 22, _did));
        set => SetMiotProperty(3, 22, _did, value);
    }

    /// <summary>
    /// Get delay time before arming mode becomes effective
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <returns>delay time in seconds (range 0-60)</returns>
    public byte GetDelayTimeForArmingMode(ArmingModeValue armingMode)
    {
        return byte.Parse(GetMiotProperty(3, _delayTimeMap[armingMode], _did));
    }

    /// <summary>
    /// Set delay time for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <param name="delayTime">seconds in range 0 - 60</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetDelayTimeForArmingMode(ArmingModeValue armingMode, byte delayTime)
    {
        if (delayTime > 60) throw new ArgumentOutOfRangeException("Delay time should be in range 0 - 60");

        SetMiotProperty(3, _delayTimeMap[armingMode], _did, delayTime);
    }

    /// <summary>
    /// Get alarm duration time for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <returns>duration time in seconds (range 0-2147483647)</returns>
    public uint GetAlarmDurationForArmingMode(ArmingModeValue armingMode) => uint.Parse(GetMiotProperty(3, _alarmDurationMap[armingMode], _did));

    /// <summary>
    /// Set delay time for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <param name="durationTime">seconds in range 0 - 2147483647</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetAlarmDurationForArmingMode(ArmingModeValue armingMode, uint durationTime)
    {
        if (durationTime > int.MaxValue) throw new ArgumentOutOfRangeException("Duration time should be in range 0 - 2147483647");

        SetMiotProperty(3, _alarmDurationMap[armingMode], _did, durationTime);
    }

    /// <summary>
    /// Get alarm volume for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <returns>Silent, Low, Middle, High</returns>
    public VolumeLeveValue GetAlarmVolumeForArmingMode(ArmingModeValue armingMode) => (VolumeLeveValue)byte.Parse(GetMiotProperty(3, _alarmVolumeMap[armingMode], _did));

    /// <summary>
    /// Set alarm volume for specific arming mode
    /// </summary>
    /// <param name="armingMode">Basic, Home, Away, Sleep</param>
    /// <param name="volumeLevel">Silent, Low, Middle, High</param>
    public void SetAlarmVolumeForArmingMode(ArmingModeValue armingMode, VolumeLeveValue volumeLevel)
    {
        SetMiotProperty(3, _alarmVolumeMap[armingMode], _did, volumeLevel);
    }

    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
