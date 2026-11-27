using MiHomeLib.Protocols;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>MFKZQ01LM cube shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL_LEGACY)]
public sealed class AqaraCubeSensor : ProtocolMappedDeviceBase, IBatteryPowered
{
    public const string MARKET_MODEL = "MFKZQ01LM";
    public const string XIAOMI_GATEWAY2_MODEL = "sensor_cube.aqgl01";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_cube.aqgl01";
    public const string LUMI_MQTT_MODEL_LEGACY = "lumi.sensor_cube";

    // Action codes of the 13.1.85 resource on Lumi MQTT gateways. Not verified on a real cube yet:
    // an unknown code is logged with its value
    private static readonly System.Collections.Generic.Dictionary<int, CubeStatus> LumiActions = new()
    {
        [0] = CubeStatus.Flip90,
        [1] = CubeStatus.Flip180,
        [2] = CubeStatus.Move,
        [3] = CubeStatus.TapTwice,
        [16] = CubeStatus.Rotated,
        [20] = CubeStatus.ShakeAir,
    };

    // Values of one MQTT report, combined in OnMessageParsed: a rotation comes as action, angle and duration together
    private CubeStatus? _reportedAction;
    private float? _reportedAngle;
    private int? _reportedDuration;

    public enum CubeStatus
    {
        Undefined,
        Rotated,
        Move,
        Flip90,
        Flip180,
        TapTwice,
        ShakeAir,
        Swing,
        Alert,
        FreeFall,
    }

    public override string DeviceId { get; }
    public int ShortId { get; }

    /// <summary>
    /// The last action of the cube, <see cref="CubeStatus.Undefined"/> until the first one
    /// </summary>
    public CubeStatus Status { get; private set; } = CubeStatus.Undefined;

    /// <summary>
    /// The last rotation, set together with <see cref="CubeStatus.Rotated"/>:
    /// <see cref="CubeRotate.Shift"/> is the angle in degrees, <see cref="CubeRotate.Value"/> the duration in milliseconds
    /// </summary>
    public CubeRotate Rotate { get; private set; }

    public float Voltage { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte BatteryPercent { get; private set; }

    /// <summary>
    /// Raised on every action of the cube, the action is passed as an argument
    /// </summary>
    public event Func<CubeStatus, Task> OnStatusChangedAsync = _ => Task.CompletedTask;

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

    public AqaraCubeSensor(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", value =>
        {
            var status = value.ReadString() switch
            {
                "move" => CubeStatus.Move,
                "flip90" => CubeStatus.Flip90,
                "flip180" => CubeStatus.Flip180,
                "tap_twice" => CubeStatus.TapTwice,
                "shake_air" => CubeStatus.ShakeAir,
                "swing" => CubeStatus.Swing,
                "alert" => CubeStatus.Alert,
                "free_fall" => CubeStatus.FreeFall,
                _ => CubeStatus.Undefined,
            };

            if (status == CubeStatus.Undefined)
                _logger.LogWarning("Cube action '{Action}' is not supported yet. Please contribute to support.", value.GetRawText());

            UpdateStatus(status);
        });
        map.XiaomiGateway2("rotate", UpdateGateway2Rotate);
        map.XiaomiGateway2("voltage", value => UpdateVoltage(value.ReadInt32() / 1000f));

        // Resources used by the AlexxIT/XiaomiGateway3 integration
        map.LumiMqtt("13.1.85", value =>
        {
            var code = value.ReadInt32();
            if (LumiActions.TryGetValue(code, out var action))
            {
                _reportedAction = action;
                return;
            }

            _logger.LogWarning("Cube action code '{Code}' is not supported yet. Please contribute to support.", code);
            _reportedAction = CubeStatus.Undefined;
        });
        map.LumiMqtt("0.3.85", value => _reportedAngle = (value.ReadDouble() * 0.001).ToFiniteSingle());
        map.LumiMqtt("0.2.85", value => _reportedDuration = value.ReadInt32());
        map.LumiMqtt("8.0.2008", "voltage",
            value => UpdateVoltage(value.ReadInt32() / 1000f),
            value => Voltage = value.GetValue<int>() / 1000f);
        map.LumiMqtt("8.0.2001", "battery",
            value => SetAndRaise(nameof(OnBatteryPercentChangeAsync), BatteryPercent, () => BatteryPercent = value.ReadByte(), OnBatteryPercentChangeAsync),
            value => BatteryPercent = checked((byte)value.GetValue<int>()));
    }

    private protected override void OnMessageParsing() =>
        (_reportedAction, _reportedAngle, _reportedDuration) = (null, null, null);

    private protected override void OnMessageParsed()
    {
        var action = _reportedAction;
        var angle = _reportedAngle;
        var duration = _reportedDuration;
        (_reportedAction, _reportedAngle, _reportedDuration) = (null, null, null);

        if (action is null) return;

        if (action == CubeStatus.Rotated)
            Rotate = new CubeRotate((short)Math.Round(angle ?? 0), (short)Math.Min(duration ?? 0, short.MaxValue));

        UpdateStatus(action.Value);
    }

    private void UpdateVoltage(float value) => SetAndRaise(nameof(OnVoltageChangeAsync), Voltage, () => Voltage = value, OnVoltageChangeAsync);


    // "shift,value", e.g. "90,500"
    private void UpdateGateway2Rotate(JsonElement value)
    {
        var parts = value.ValueKind == JsonValueKind.String ? value.ReadString().Split(',') : [];
        if (parts.Length != 2 ||
            !short.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var shift) ||
            !short.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rotateValue))
        {
            _logger.LogWarning("Rotation '{Value}' is not 'shift,value'", value.GetRawText());
            return;
        }

        Rotate = new CubeRotate(shift, rotateValue);
        UpdateStatus(CubeStatus.Rotated);
    }

    private void UpdateStatus(CubeStatus status)
    {
        Status = status;
        RaiseEvent(nameof(OnStatusChangedAsync), OnStatusChangedAsync, status);
    }

    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MARKET_MODEL} {Model ?? LUMI_MQTT_MODEL} {DeviceId}, Voltage: {Voltage}V, BatteryPercent: {BatteryPercent}%, " +
                   $"Rotate: [{Rotate}], Status: {Status}";

        return $"Device: {MARKET_MODEL} {Model ?? XIAOMI_GATEWAY2_MODEL} {DeviceId}, Voltage: {Voltage}V, Rotate: [{Rotate}], Status: {Status}";
    }

    public readonly struct CubeRotate(short shift, short value)
    {
        public short Shift { get; } = shift;

        public short Value { get; } = value;

        public override string ToString() => $"{Shift},{Value}";
    }
}
