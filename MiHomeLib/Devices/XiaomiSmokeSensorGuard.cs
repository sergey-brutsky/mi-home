using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>JTYJ-GD-03MI BLE smoke sensor used by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiBle, MODEL, PDID)]
public sealed class XiaomiSmokeSensorGuard : ProtocolMappedDeviceBase, IProtocolMappedBleDevice
{
    public const string MARKET_MODEL = "JTYJ-GD-03MI";
    public const string MODEL = "lumi.sensor_smoke.mcn02";
    public const int PDID = 2455;
    private const int SMOKE_DETECTED_EID = 4117;
    private const int BATTERY_EID = 4106;
    private bool _hasBatteryReport;

    public enum SmokeState
    {
        Unknown = -1,
        SmokeDetected = 1,
        NoSmokeDetected = 0,
    }

    public override string DeviceId { get; }
    public string Mac { get; private set; }
    public SmokeState Smoke { get; private set; } = SmokeState.Unknown;
    public byte BatteryPercent { get; private set; }

    /// <summary>Raised only when the smoke state changes. Passes the previous state, the new state is in Smoke.</summary>
    public event Func<SmokeState, Task> OnSmokeChangeAsync = _ => Task.CompletedTask;

    /// <summary>Raised on every battery report, including unchanged values. Passes the previous value, the new value is in BatteryPercent.</summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = _ => Task.CompletedTask;

    public XiaomiSmokeSensorGuard(string deviceId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
    }

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiBle(SMOKE_DETECTED_EID, value =>
        {
            var code = BleValueDecoder.DecodeByte(value.ReadString(), "a smoke state");
            var smoke = Enum.IsDefined(typeof(SmokeState), (int)code) ? (SmokeState)code : SmokeState.Unknown;
            if (Smoke == smoke) return;

            var oldSmoke = Smoke;
            Smoke = smoke;
            RaiseEvent(OnSmokeChangeAsync, oldSmoke);
        });
        map.LumiBle(BATTERY_EID, value =>
        {
            var battery = BleValueDecoder.DecodeByte(value.ReadString(), "a battery value");
            var oldBattery = BatteryPercent;
            BatteryPercent = battery;
            _hasBatteryReport = true;
            RaiseEvent(OnBatteryPercentChangeAsync, oldBattery);
        });
    }

    public override string ToString()
    {
        var battery = _hasBatteryReport ? $"{BatteryPercent}%" : "Unknown";
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}, Smoke state: {Smoke}, BatteryPercent: {battery}";
    }
}
