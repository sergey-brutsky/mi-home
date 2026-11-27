using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// WXKG20LM E1 lumi.remote.acn007 wireless mini switch (CN revision)
/// </summary>
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraWirelessMiniSwitchCN2 : ProtocolMappedDeviceBase
{
    public const string MARKET_MODEL = "WXKG20LM";
    public const string LUMI_MQTT_MODEL = "lumi.remote.acn007";
    private const int CLICK_SIID = 2; // click
    private const int BATTERY_SIID = 3; // battery & percentage
    private const int BATTERY_STATUS_PIID = 1; // state
    private const int BATTERY_VOLTAGE_PIID = 2; // voltage

    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        LongPressClick = 3 // press & hold more than 3 seconds
    }

    public enum BatteryStatusType
    {
        Unknown = 0,
        Normal = 1,
        LowBattery = 2
    }

    public override string DeviceId { get; }
    public int ShortId { get; }
    /// <summary>Battery voltage in volts; 0 until the first report.</summary>
    public float Voltage { get; private set; }
    /// <summary>Unknown until the first recognized battery status report.</summary>
    public BatteryStatusType BatteryStatus { get; private set; } = BatteryStatusType.Unknown;

    public AqaraWirelessMiniSwitchCN2(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiMqttMiSpecEvent(CLICK_SIID, 1, _ => RaiseEvent(nameof(OnClickAsync), OnClickAsync, ClickArg.SingleClick));
        map.LumiMqttMiSpecEvent(CLICK_SIID, 2, _ => RaiseEvent(nameof(OnClickAsync), OnClickAsync, ClickArg.DoubleClick));
        map.LumiMqttMiSpecEvent(CLICK_SIID, 3, _ => RaiseEvent(nameof(OnClickAsync), OnClickAsync, ClickArg.LongPressClick));
        map.LumiMqttMiSpec(BATTERY_SIID, BATTERY_VOLTAGE_PIID,
            value => SetAndRaise(nameof(OnVoltageChangeAsync), Voltage, () => Voltage = value.ReadInt32() / 1000f, OnVoltageChangeAsync));
        map.LumiMqttMiSpec(BATTERY_SIID, BATTERY_STATUS_PIID, value =>
        {
            var code = value.ReadInt32();
            BatteryStatus = Enum.IsDefined(typeof(BatteryStatusType), code) ? (BatteryStatusType)code : BatteryStatusType.Unknown;
            RaiseEvent(nameof(OnBatteryStatusAsync), OnBatteryStatusAsync, BatteryStatus);
        });
    }

    /// <summary>
    /// Value of click type passed as a parameter
    /// </summary>
    public event Func<ClickArg, Task> OnClickAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Raised on every voltage report, including unchanged values. Passes the previous voltage; the new value is in Voltage.
    /// </summary>
    public event Func<float, Task> OnVoltageChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Raised on every battery status report. Passes the new status, also available in BatteryStatus.
    /// </summary>
    public event Func<BatteryStatusType, Task> OnBatteryStatusAsync = (_) => Task.CompletedTask;

    public override string ToString() => GetBaseInfo(MARKET_MODEL, LUMI_MQTT_MODEL) + $"Voltage: {Voltage}V, BatteryStatus: {BatteryStatus}";
}
