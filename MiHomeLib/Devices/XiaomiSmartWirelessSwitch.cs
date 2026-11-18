using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
/// <summary>WXKG01LM wireless switch shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
public sealed class XiaomiSmartWirelessSwitch : LumiBatteryDeviceBase
{
    public const string MARKET_MODEL = "WXKG01LM";
    public const string XIAOMI_GATEWAY2_MODEL = "switch";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_switch";

    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        TripleClick = 3,
        QuadrupleClick = 4,
        ManyClicks = 128,
        LongPressHold = 16,
        LongPressRelease = 17
    }

    public override string DeviceId { get; }
    public int ShortId { get; }
    public string Status { get; private set; }

    public event Func<ClickArg, Task> OnClickAsync = _ => Task.CompletedTask;

    public XiaomiSmartWirelessSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", value => UpdateStatus(value.ReadString()));

        map.LumiMqtt("13.1.85", value => UpdateClick(value.ReadInt32()));
        MapBattery(map);
    }

    private void UpdateStatus(string status)
    {
        Status = status;
        var click = status switch
        {
            "click" => ClickArg.SingleClick,
            "double_click" => ClickArg.DoubleClick,
            "long_click_press" => ClickArg.LongPressHold,
            "long_click_release" => ClickArg.LongPressRelease,
            _ => (ClickArg?)null
        };

        if (click.HasValue)
            RaiseEvent(OnClickAsync, click.Value);
    }

    private void UpdateClick(int value)
    {
        if (Enum.IsDefined(typeof(ClickArg), value))
            RaiseEvent(OnClickAsync, (ClickArg)value);
    }





    public override string ToString()
    {
        var model = Model ?? LUMI_MQTT_MODEL;
        return $"Device: {MARKET_MODEL} {model} {DeviceId}, Voltage: {Voltage}V, " +
        $"BatteryPercent: {BatteryPercent}%, ChipTemperature: {ChipTemperature}°C, " +
        $"LinkQuality: {LinkQuality}";
    }
}
