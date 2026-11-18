using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>SJCGQ11LM water leak sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraWaterLeakSensor : LumiBatteryDeviceBase
{
    public const string MARKET_MODEL = "SJCGQ11LM";
    public const string XIAOMI_GATEWAY2_MODEL = "sensor_wleak.aq1";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_wleak.aq1";

    public override string DeviceId { get; }
    public int ShortId { get; }
    public bool Moisture { get; private set; }

    /// <summary>
    /// Raised when the leak state is reported. Unlike the value change events,
    /// the new state is passed as an argument: true means a leak is detected
    /// </summary>
    public event Func<bool, Task> OnMoistureChangeAsync = _ => Task.CompletedTask;

    public AqaraWaterLeakSensor(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", UpdateGateway2Status);

        map.LumiMqtt("3.1.85", value => UpdateMoisture(value.ReadInt32() == 1));
        MapBattery(map);
    }

    private void UpdateGateway2Status(System.Text.Json.JsonElement value)
    {
        switch (value.ReadString())
        {
            case "leak":
                UpdateMoisture(true);
                break;
            case "no_leak":
                UpdateMoisture(false);
                break;
        }
    }

    private void UpdateMoisture(bool value)
    {
        Moisture = value;
        RaiseEvent(OnMoistureChangeAsync, value);
    }





    public override string ToString()
    {
        var model = Model ?? LUMI_MQTT_MODEL;
        var description = $"Device: {MARKET_MODEL} {model} {DeviceId}, Moisture: {Moisture}, ";
        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return description + $"Voltage: {Voltage}V";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return description +
                   $"Voltage: {Voltage}V, BatteryPercent: {BatteryPercent}%, " +
                   $"LinkQuality: {LinkQuality}, ChipTemperature: {ChipTemperature}°C";

        return description.TrimEnd(',', ' ');
    }
}
