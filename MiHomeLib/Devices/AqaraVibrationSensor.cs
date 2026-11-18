using MiHomeLib.Protocols;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib.Devices;

/// <summary>DJT11LM vibration sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraVibrationSensor : LumiBatteryDeviceBase, IProtocolMappedManageableDevice
{
    public const string MARKET_MODEL = "DJT11LM";
    public const string XIAOMI_GATEWAY2_MODEL = "vibration";
    public const string LUMI_MQTT_MODEL = "lumi.vibration.aq1";

    public enum SensivityState
    {
        Low = 21,
        Middle = 11,
        High = 1,
    }

    private const string SENSIVITY_RES_NAME = "14.1.85";
    private ZigBeeTransport _zigBeeTransport;

    public override string DeviceId { get; }
    public int ShortId { get; }

    /// <summary>
    /// Tilt angle 0-180 after the last tilt, 0 until the first report
    /// </summary>
    public int FinalTiltAngle { get; private set; }

    /// <summary>
    /// Sensor orientation reported by Xiaomi Gateway 2, (0, 0, 0) until the first report and on Lumi MQTT gateways
    /// </summary>
    public (int X, int Y, int Z) Coordinates { get; private set; }

    /// <summary>
    /// Bed activity 0-255. The vendor does not document it, see
    /// https://github.com/Danielhiversen/PyXiaomiGateway/issues/86#issuecomment-418135852
    /// </summary>
    public int BedActivity { get; private set; }





    public event Func<Task> OnVibrationAsync = () => Task.CompletedTask;
    public event Func<Task> OnTiltAsync = () => Task.CompletedTask;
    public event Func<Task> OnFreeFallAsync = () => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the tilt angle, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="FinalTiltAngle"/>
    /// </summary>
    public event Func<int, Task> OnFinalTiltAngleAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the coordinates (Xiaomi Gateway 2 only), even if they have not changed.
    /// The previous value is passed as an argument, the new one is in <see cref="Coordinates"/>
    /// </summary>
    public event Func<(int X, int Y, int Z), Task> OnCoordinatesChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the bed activity, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="BedActivity"/>
    /// </summary>
    public event Func<int, Task> OnBedActivityAsync = _ => Task.CompletedTask;





    public AqaraVibrationSensor(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) => _zigBeeTransport = new ZigBeeTransport(mqttTransport);

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", value =>
        {
            switch (value.ReadString())
            {
                case "vibrate": RaiseEvent(OnVibrationAsync); break;
                case "tilt": RaiseEvent(OnTiltAsync); break;
                case "free_fall": RaiseEvent(OnFreeFallAsync); break;
            }
        });
        map.XiaomiGateway2("final_tilt_angle", value => { if (TryGetGateway2Int(value, out var angle)) UpdateFinalTiltAngle(angle); });
        map.XiaomiGateway2("coordination", UpdateGateway2Coordinates);
        map.XiaomiGateway2("bed_activity", value => { if (TryGetGateway2Int(value, out var activity)) UpdateBedActivity(activity); });

        map.LumiMqtt("13.1.85", value =>
        {
            switch (value.ReadInt32())
            {
                case 1: RaiseEvent(OnVibrationAsync); break;
                case 2: RaiseEvent(OnTiltAsync); break;
                case 3: RaiseEvent(OnFreeFallAsync); break;
            }
        });
        map.LumiMqtt("0.2.85", value => UpdateFinalTiltAngle(value.ReadInt32()));
        map.LumiMqtt("0.1.85", value => UpdateBedActivity(value.ReadInt32()));
        MapBattery(map);
    }

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void SetSensivity(SensivityState state) => RunSync(() => SetSensivityAsync(state));

    /// <inheritdoc cref="SetSensivity"/>
    public Task SetSensivityAsync(SensivityState state)
    {
        try
        {
            if (_zigBeeTransport is null)
                throw new NotSupportedException($"Device '{DeviceId}' accepts commands only on Lumi MQTT gateways (Multimode Gateway, Aqara Hub E1).");

            _zigBeeTransport.SendWriteCommand(DeviceId, SENSIVITY_RES_NAME, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }


    // "x,y,z"
    private void UpdateGateway2Coordinates(JsonElement value)
    {
        var parts = value.ValueKind == JsonValueKind.String ? value.ReadString().Split(',') : [];
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
        {
            _logger.LogWarning("Coordinates '{Value}' are not 'x,y,z'", value.GetRawText());
            return;
        }

        SetAndRaise(Coordinates, () => Coordinates = (x, y, z), OnCoordinatesChangeAsync);
    }

    private void UpdateFinalTiltAngle(int value) => SetAndRaise(FinalTiltAngle, () => FinalTiltAngle = value, OnFinalTiltAngleAsync);

    private void UpdateBedActivity(int value) => SetAndRaise(BedActivity, () => BedActivity = value, OnBedActivityAsync);





    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return $"Device: {MARKET_MODEL} {Model ?? XIAOMI_GATEWAY2_MODEL} {DeviceId}, Voltage: {Voltage}V, FinalTiltAngle: {FinalTiltAngle}";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MARKET_MODEL} {Model ?? LUMI_MQTT_MODEL} {DeviceId}, Voltage: {Voltage}V, BatteryPercent: {BatteryPercent}%, " +
                   $"FinalTiltAngle: {FinalTiltAngle}, ChipTemperature: {ChipTemperature}°C, LinkQuality: {LinkQuality}";

        return $"Device: {MARKET_MODEL} {DeviceId}";
    }
}
