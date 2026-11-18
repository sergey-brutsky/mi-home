using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
/// <summary>
/// WSDCGQ01LM temperature and humidity sensor, shared by Xiaomi Gateway 2 and Lumi MQTT gateways.
/// </summary>
public sealed class XiaomiTemperatureHumiditySensor : LumiBatteryDeviceBase
{
    public const string MARKET_MODEL = "WSDCGQ01LM";
    public const string XIAOMI_GATEWAY2_MODEL = "sensor_ht";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_ht";

    public override string DeviceId { get; }
    public int ShortId { get; }
    public float Temperature { get; private set; }
    public float Humidity { get; private set; }

    /// <summary>
    /// Raised on every report of the temperature, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Temperature"/>
    /// </summary>
    public event Func<float, Task> OnTemperatureChangeAsync = _ => Task.CompletedTask;
    /// <summary>
    /// Raised on every report of the relative humidity, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Humidity"/>
    /// </summary>
    public event Func<float, Task> OnHumidityChangeAsync = _ => Task.CompletedTask;

    public XiaomiTemperatureHumiditySensor(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("temperature", value => UpdateTemperature(int.Parse(value.ReadString()) / 100f));
        map.XiaomiGateway2("humidity", value => UpdateHumidity(int.Parse(value.ReadString()) / 100f));

        map.LumiMqtt("0.1.85", "temperature",
            value => UpdateTemperature(value.ReadInt32() / 100f),
            value => Temperature = value.GetValue<int>() / 100f);
        map.LumiMqtt("0.2.85", "humidity",
            value => UpdateHumidity(value.ReadInt32() / 100f),
            value => Humidity = value.GetValue<int>() / 100f);
        MapBattery(map, InitialBatteryValues.BatteryPercent);
    }

    internal void UpdateTemperature(float value)
    {
        SetAndRaise(Temperature, () => Temperature = value, OnTemperatureChangeAsync);
    }

    internal void UpdateHumidity(float value)
    {
        SetAndRaise(Humidity, () => Humidity = value, OnHumidityChangeAsync);
    }





    public override string ToString()
    {
        var model = Model ?? LUMI_MQTT_MODEL;
        var description =
            $"Device: {MARKET_MODEL} {model} {DeviceId}, Temperature: {Temperature}°C, Humidity: {Humidity}%, ";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return description + $"BatteryPercent: {BatteryPercent}%";

        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return description + $"Voltage: {Voltage}V";

        return description.TrimEnd(' ');
    }
}
