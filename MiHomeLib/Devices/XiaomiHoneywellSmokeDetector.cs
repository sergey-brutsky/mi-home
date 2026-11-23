using MiHomeLib.Protocols;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib.Devices;

/// <summary>JTYJ-GD-01LM/BW smoke detector shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class XiaomiHoneywellSmokeDetector : ProtocolMappedDeviceBase, IProtocolMappedManageableDevice
{
    public const string MARKET_MODEL = "JTYJ-GD-01LM/BW";
    public const string XIAOMI_GATEWAY2_MODEL = "smoke";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_smoke";
    public const int SELF_TEST_MAGIC_NUMBER = 50397184; // only God knows why this number

    public enum SensitivityMode
    {
        // I don't know why exactly these numbers have been selected by vendor
        NoSmoke = 67174400,
        LowSmoke = 67239936,
        MiddleSmoke = 67305472,
    }

    [Obsolete("Use SensitivityMode. SensivityMode will be removed in the next major version")]
    public enum SensivityMode
    {
        // I don't know why exactly these numbers have been selected by vendor
        NoSmoke = 67174400,
        LowSmoke = 67239936,
        MiddleSmoke = 67305472,
    }

    private const string SMOKE_MODE_RES_NAME = "14.1.85";
    private ZigBeeTransport _zigBeeTransport;

    public override string DeviceId { get; }
    public int ShortId { get; }

    /// <summary>
    /// True while the detector reports smoke
    /// </summary>
    public bool Alarm { get; private set; }

    /// <summary>
    /// Smoke density as the detector reports it, 0 until the first report
    /// </summary>
    public int SmokeDensity { get; private set; }

    /// <summary>
    /// Reported by Xiaomi Gateway 2 always, by Lumi MQTT gateways only if the detector sends it
    /// </summary>
    public float Voltage { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte LinkQuality { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte ChipTemperature { get; private set; }

    /// <summary>
    /// Raised when the detector reports smoke
    /// </summary>
    public event Func<Task> OnAlarmAsync = () => Task.CompletedTask;

    /// <summary>
    /// Raised when the detector reports there is no smoke (anymore)
    /// </summary>
    public event Func<Task> OnAlarmStoppedAsync = () => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the smoke density, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="SmokeDensity"/>
    /// </summary>
    public event Func<int, Task> OnSmokeDensityChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised when Lumi MQTT gateways report the sensitivity mode, the new mode is passed as an argument
    /// </summary>
    [Obsolete("Use OnSmokeSensitivityModeChangedAsync. OnSmokeSensivityModeChanged will be removed in the next major version")]
#pragma warning disable CS0618
    public event Func<SensivityMode, Task> OnSmokeSensivityModeChanged = _ => Task.CompletedTask;
#pragma warning restore CS0618

    /// <summary>Raised when Lumi MQTT gateways report the sensitivity mode; the new mode is passed as an argument.</summary>
    public event Func<SensitivityMode, Task> OnSmokeSensitivityModeChangedAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the voltage, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Voltage"/>
    /// </summary>
    public event Func<float, Task> OnVoltageChangeAsync = _ => Task.CompletedTask;

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

    public XiaomiHoneywellSmokeDetector(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) => _zigBeeTransport = new ZigBeeTransport(mqttTransport);

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("alarm", value => { if (TryGetGateway2Int(value, out var alarm)) UpdateAlarm(alarm == 1); });
        map.XiaomiGateway2("density", value => { if (TryGetGateway2Int(value, out var density)) UpdateSmokeDensity(density); });
        map.XiaomiGateway2("voltage", value => UpdateVoltage(value.ReadInt32() / 1000f));

        map.LumiMqtt("13.1.85", value => UpdateAlarm(value.ReadInt32() == 1));
        map.LumiMqtt("0.1.85", "density",
            value => UpdateSmokeDensity(value.ReadInt32()),
            value => SmokeDensity = value.GetValue<int>());
        map.LumiMqtt(SMOKE_MODE_RES_NAME, value => ReportSensitivity(value.ReadInt32()));
        // Not read at discovery: the detector was never asked for it, it is taken when it comes with a report
        map.LumiMqtt("8.0.2008", value => UpdateVoltage(value.ReadInt32() / 1000f));
        map.LumiMqtt("8.0.2007", "lqi",
            value => SetAndRaise(LinkQuality, () => LinkQuality = value.ReadByte(), OnLinkQualityChangeAsync),
            value => LinkQuality = checked((byte)value.GetValue<int>()));
        map.LumiMqtt("8.0.2006", "chip_temperature",
            value => SetAndRaise(ChipTemperature, () => ChipTemperature = value.ReadByte(), OnChipTemperatureChangeAsync),
            value => ChipTemperature = checked((byte)value.GetValue<int>()));
    }

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    [Obsolete("Use SetSensitivity. SetSensivity will be removed in the next major version")]
#pragma warning disable CS0618
    public void SetSensivity(SensivityMode mode) => SetSensitivity((SensitivityMode)(int)mode);

    [Obsolete("Use SetSensitivityAsync. SetSensivityAsync will be removed in the next major version")]
    public Task SetSensivityAsync(SensivityMode mode) => SetSensitivityAsync((SensitivityMode)(int)mode);
#pragma warning restore CS0618

    /// <summary>Sets sensitivity on Lumi MQTT gateways.</summary>
    public void SetSensitivity(SensitivityMode mode) => RunSync(() => SetSensitivityAsync(mode));

    /// <inheritdoc cref="SetSensitivity"/>
    public Task SetSensitivityAsync(SensitivityMode mode)
    {
        try
        {
            SendWriteCommand((int)mode);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void RunSelfTest() => RunSync(() => RunSelfTestAsync());

    /// <inheritdoc cref="RunSelfTest"/>
    public Task RunSelfTestAsync()
    {
        try
        {
            SendWriteCommand(SELF_TEST_MAGIC_NUMBER);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    private void ReportSensitivity(int value) => _ = ReportSensitivityAsync(value);

    private async Task ReportSensitivityAsync(int value)
    {
        await RaiseEventAsync(OnSmokeSensitivityModeChangedAsync, (SensitivityMode)value).ConfigureAwait(false);
#pragma warning disable CS0618
        await RaiseEventAsync(OnSmokeSensivityModeChanged, (SensivityMode)value).ConfigureAwait(false);
#pragma warning restore CS0618
    }

    private void SendWriteCommand(int value)
    {
        if (_zigBeeTransport is null)
            throw new NotSupportedException($"Device '{DeviceId}' accepts commands only on Lumi MQTT gateways (Multimode Gateway, Aqara Hub E1).");

        _zigBeeTransport.SendWriteCommand(DeviceId, SMOKE_MODE_RES_NAME, value);
    }


    private void UpdateAlarm(bool alarm)
    {
        Alarm = alarm;
        RaiseEvent(alarm ? OnAlarmAsync : OnAlarmStoppedAsync);
    }

    private void UpdateSmokeDensity(int value) => SetAndRaise(SmokeDensity, () => SmokeDensity = value, OnSmokeDensityChangeAsync);

    private void UpdateVoltage(float value) => SetAndRaise(Voltage, () => Voltage = value, OnVoltageChangeAsync);



    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return $"Device: {MARKET_MODEL} {Model ?? XIAOMI_GATEWAY2_MODEL} {DeviceId}, Voltage: {Voltage}V, Alarm: {Alarm}, Smoke density: {SmokeDensity}";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MARKET_MODEL} {Model ?? LUMI_MQTT_MODEL} {DeviceId}, Alarm: {Alarm}, Smoke density: {SmokeDensity}, " +
                   $"ChipTemperature: {ChipTemperature}°C, LinkQuality: {LinkQuality}";

        return $"Device: {MARKET_MODEL} {DeviceId}";
    }
}
