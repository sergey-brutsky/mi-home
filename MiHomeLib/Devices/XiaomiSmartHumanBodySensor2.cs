using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>RTCGQ02LM BLE motion and light sensor used by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiBle, MODEL, PDID)]
public sealed class XiaomiSmartHumanBodySensor2 : ProtocolMappedDeviceBase, IProtocolMappedBleDevice, IBatteryPowered
{
    public const string MARKET_MODEL = "RTCGQ02LM";
    public const string MODEL = "lumi.motion.bmgl01";
    public enum MotionState
    {
        Unknown = -1,
        MotionWithoutLight = 0,
        MotionWithLight = 100,
    }
    public enum NoMotionState
    {   // Only these intervals are exposed by the sensor
        Idle120Seconds = 120,
        Idle300Seconds = 300,
    }
    public enum LightState
    {
        Unknown = -1,
        LightOff = 0,
        LightOn = 1,
    }
    public const int PDID = 2701;
    private const int MOTION_EID = 15;
    private const int LIGHT_EID = 4120;
    private const int IDLE_TIME_EID = 4119;
    public MotionState Motion { get; private set; } = MotionState.Unknown;
    /// <summary>Raised only when the reported motion state changes. Passes the previous state.</summary>
    public event Func<MotionState, Task> OnMotionDetectedAsync = (_) => Task.CompletedTask;
    public LightState Light { get; private set; } = LightState.Unknown;
    /// <summary>Raised only when light changes. Passes the previous state.</summary>
    public event Func<LightState, Task> OnLightChangeAsync = (_) => Task.CompletedTask;
    /// <summary>Raised on every idle-time report. Passes the reported duration in seconds.</summary>
    public event Func<NoMotionState, Task> OnNoMotionDetectedAsync = (_) => Task.CompletedTask;
    private bool _hasBatteryReport;
    public override string DeviceId { get; }
    public string Mac { get; private set; }
    public byte BatteryPercent { get; private set; }

    /// <summary>Raised on every battery report. Passes the previous value, the new value is in BatteryPercent.</summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = _ => Task.CompletedTask;

    public XiaomiSmartHumanBodySensor2(string deviceId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
    }

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiBle(MOTION_EID, data =>
        {
            var code = BleValueDecoder.DecodeUnsignedLittleEndian(data.ReadString(), 3);
            var motion = code switch
            {
                0 => MotionState.MotionWithoutLight,
                256 => MotionState.MotionWithLight,
                _ => MotionState.Unknown
            };
            if (motion == Motion) return;
            var oldMotion = Motion;
            Motion = motion;
            RaiseEvent(nameof(OnMotionDetectedAsync), OnMotionDetectedAsync, oldMotion);
        });
        map.LumiBle(LIGHT_EID, data =>
        {
            var code = BleValueDecoder.DecodeByte(data.ReadString(), "a light state");
            var light = code switch { 0 => LightState.LightOff, 1 => LightState.LightOn, _ => LightState.Unknown };
            if (light == Light) return;
            var oldLight = Light;
            Light = light;
            RaiseEvent(nameof(OnLightChangeAsync), OnLightChangeAsync, oldLight);
        });
        map.LumiBle(IDLE_TIME_EID, data =>
        {
            var seconds = BleValueDecoder.DecodeUnsignedLittleEndian(data.ReadString(), 4);
            RaiseEvent(nameof(OnNoMotionDetectedAsync), OnNoMotionDetectedAsync, (NoMotionState)seconds);
        });
        map.LumiBle(4106, data =>
        {
            var battery = BleValueDecoder.DecodeByte(data.ReadString(), "a battery value");
            var oldBattery = BatteryPercent;
            BatteryPercent = battery;
            _hasBatteryReport = true;
            RaiseEvent(nameof(OnBatteryPercentChangeAsync), OnBatteryPercentChangeAsync, oldBattery);
        });
    }

    public override string ToString()
    {
        var battery = _hasBatteryReport ? $"{BatteryPercent}%" : "Unknown";
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}, Motion: {Motion}, Light: {Light}, BatteryPercent: {battery}";
    }
}
