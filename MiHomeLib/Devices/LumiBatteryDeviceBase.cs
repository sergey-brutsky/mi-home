using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Protocols;

namespace MiHomeLib.Devices;

/// <summary>
/// Battery and radio values of Aqara/Xiaomi Zigbee sensors: Xiaomi Gateway 2 reports the voltage only,
/// Lumi MQTT gateways also the battery percent, the link quality and the chip temperature
/// </summary>
public abstract class LumiBatteryDeviceBase : ProtocolMappedDeviceBase
{
    /// <summary>
    /// Which battery values Lumi MQTT gateways are asked for at discovery. Values that are not asked for still come with reports
    /// </summary>
    [Flags]
    private protected enum InitialBatteryValues
    {
        None = 0,
        Voltage = 1,
        BatteryPercent = 2,
        LinkQuality = 4,
        ChipTemperature = 8,
        All = Voltage | BatteryPercent | LinkQuality | ChipTemperature,
    }

    private protected LumiBatteryDeviceBase(ILoggerFactory loggerFactory) : base(loggerFactory) { }

    public float Voltage { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte BatteryPercent { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte LinkQuality { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte ChipTemperature { get; private set; }

    /// <summary>
    /// Raised on every report of the voltage, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Voltage"/>
    /// </summary>
    public event Func<float, Task> OnVoltageChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the battery percent, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="BatteryPercent"/>
    /// </summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the link quality, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="LinkQuality"/>
    /// </summary>
    public event Func<byte, Task> OnLinkQualityChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the chip temperature, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="ChipTemperature"/>
    /// </summary>
    public event Func<byte, Task> OnChipTemperatureChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Maps the voltage of Xiaomi Gateway 2 and the battery values of Lumi MQTT gateways.
    /// Initial values are read in the order voltage, battery, lqi, chip_temperature, after the ones mapped before this call
    /// </summary>
    private protected void MapBattery(ProtocolDeviceMap map, InitialBatteryValues initial = InitialBatteryValues.All, bool gateway2Voltage = true)
    {
        if (gateway2Voltage)
            map.XiaomiGateway2("voltage", value => UpdateVoltage(value.ReadInt32() / 1000f));

        Map(map, "8.0.2008", "voltage", initial.HasFlag(InitialBatteryValues.Voltage),
            value => UpdateVoltage(value.ReadInt32() / 1000f), value => Voltage = value / 1000f);
        Map(map, "8.0.2001", "battery", initial.HasFlag(InitialBatteryValues.BatteryPercent),
            value => UpdateBatteryPercent(value.ReadByte()), value => BatteryPercent = checked((byte)value));
        Map(map, "8.0.2007", "lqi", initial.HasFlag(InitialBatteryValues.LinkQuality),
            value => UpdateLinkQuality(value.ReadByte()), value => LinkQuality = checked((byte)value));
        Map(map, "8.0.2006", "chip_temperature", initial.HasFlag(InitialBatteryValues.ChipTemperature),
            value => UpdateChipTemperature(value.ReadByte()), value => ChipTemperature = checked((byte)value));
    }

    private static void Map(ProtocolDeviceMap map, string resource, string propertyName, bool initial,
        Action<JsonElement> update, Action<int> initialize)
    {
        if (initial)
            map.LumiMqtt(resource, propertyName, update, value => initialize(value.GetValue<int>()));
        else
            map.LumiMqtt(resource, update);
    }

    private protected void UpdateVoltage(float value)
    {
        SetAndRaise(Voltage, () => Voltage = value, OnVoltageChangeAsync);
    }

    private protected void UpdateBatteryPercent(byte value)
    {
        SetAndRaise(BatteryPercent, () => BatteryPercent = value, OnBatteryPercentChangeAsync);
    }

    private protected void UpdateLinkQuality(byte value)
    {
        SetAndRaise(LinkQuality, () => LinkQuality = value, OnLinkQualityChangeAsync);
    }

    private protected void UpdateChipTemperature(byte value)
    {
        SetAndRaise(ChipTemperature, () => ChipTemperature = value, OnChipTemperatureChangeAsync);
    }
}
