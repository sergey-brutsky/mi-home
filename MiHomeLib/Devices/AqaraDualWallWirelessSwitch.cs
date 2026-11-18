using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>WXKG02LM wireless wall switch with two buttons shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL_86SW2)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL_86SW2_ES1)]
public sealed class AqaraDualWallWirelessSwitch : ProtocolMappedDeviceBase
{
    public const string MARKET_MODEL = "WXKG02LM";
    public const string XIAOMI_GATEWAY2_MODEL = "remote.b286acn01";
    public const string LUMI_MQTT_MODEL = "lumi.remote.b286acn01";
    public const string LUMI_MQTT_MODEL_86SW2 = "lumi.sensor_86sw2";
    public const string LUMI_MQTT_MODEL_86SW2_ES1 = "lumi.sensor_86sw2.es1";

    public enum Button
    {
        Left,
        Right,
        Both,
    }

    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        LongPressHold = 16,
        LongPressRelease = 17,
    }

    public override string DeviceId { get; }
    public int ShortId { get; }
    public float Voltage { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte BatteryPercent { get; private set; }

    /// <summary>
    /// True when Lumi MQTT gateways report a low battery, always false on Xiaomi Gateway 2
    /// </summary>
    public bool BatteryLow { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte ChipTemperature { get; private set; }

    /// <summary>
    /// Raised when a button is pressed, the button and the kind of the press are passed as arguments
    /// </summary>
    public event Func<Button, ClickArg, Task> OnButtonClickAsync = (_, _) => Task.CompletedTask;

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
    /// Raised on every report of the chip temperature, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="ChipTemperature"/>
    /// </summary>
    public event Func<byte, Task> OnChipTemperatureChangeAsync = _ => Task.CompletedTask;

    public AqaraDualWallWirelessSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("channel_0", value => RaiseGateway2Click(Button.Left, value.ReadString()));
        map.XiaomiGateway2("channel_1", value => RaiseGateway2Click(Button.Right, value.ReadString()));
        map.XiaomiGateway2("dual_channel", value => RaiseGateway2Click(Button.Both, value.ReadString()));
        map.XiaomiGateway2("voltage", value => UpdateVoltage(value.ReadInt32() / 1000f));

        // Resources used by the AlexxIT/XiaomiGateway3 integration
        map.LumiMqtt("13.1.85", value => RaiseLumiClick(Button.Left, value.ReadInt32()));
        map.LumiMqtt("13.2.85", value => RaiseLumiClick(Button.Right, value.ReadInt32()));
        map.LumiMqtt("13.5.85", value => RaiseLumiClick(Button.Both, value.ReadInt32()));
        map.LumiMqtt("8.0.2008", "voltage",
            value => UpdateVoltage(value.ReadInt32() / 1000f),
            value => Voltage = value.GetValue<int>() / 1000f);
        map.LumiMqtt("8.0.2001", "battery",
            value => SetAndRaise(BatteryPercent, () => BatteryPercent = value.ReadByte(), OnBatteryPercentChangeAsync),
            value => BatteryPercent = checked((byte)value.GetValue<int>()));
        map.LumiMqtt("8.0.9001", value => BatteryLow = value.ReadInt32() == 1);
        map.LumiMqtt("8.0.2006",
            value => SetAndRaise(ChipTemperature, () => ChipTemperature = value.ReadByte(), OnChipTemperatureChangeAsync));
    }

    private void RaiseLumiClick(Button button, int code)
    {
        if (!Enum.IsDefined(typeof(ClickArg), code))
        {
            _logger.LogWarning("Click code '{Code}' of button {Button} is not supported yet. Please contribute to support.", code, button);
            return;
        }

        RaiseEvent(OnButtonClickAsync, button, (ClickArg)code);
    }

    private void UpdateVoltage(float value) => SetAndRaise(Voltage, () => Voltage = value, OnVoltageChangeAsync);



    private void RaiseGateway2Click(Button button, string action)
    {
        ClickArg? click = action switch
        {
            "click" or "both_click" => ClickArg.SingleClick,
            "double_click" => ClickArg.DoubleClick,
            "long_click" => ClickArg.LongPressHold,
            _ => null,
        };

        if (click is null)
        {
            _logger.LogWarning("Action '{Action}' of button {Button} is not supported yet. Please contribute to support.", action, button);
            return;
        }

        RaiseEvent(OnButtonClickAsync, button, click.Value);
    }

    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MARKET_MODEL} {Model ?? LUMI_MQTT_MODEL} {DeviceId}, Voltage: {Voltage}V, BatteryPercent: {BatteryPercent}%, " +
                   $"ChipTemperature: {ChipTemperature}°C";

        return $"Device: {MARKET_MODEL} {Model ?? XIAOMI_GATEWAY2_MODEL} {DeviceId}, Voltage: {Voltage}V";
    }
}
