using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraTemperatureHumiditySensor : LumiBatteryDeviceBase, ITemperatureSensor, IHumiditySensor, IPressureSensor
{
    public const string MARKET_MODEL = "WSDCGQ11LM";
    public const string XIAOMI_GATEWAY2_MODEL = "weather.v1";
    public const string LUMI_MQTT_MODEL = "lumi.weather";

    public override string DeviceId { get; }
    public int ShortId { get; }
    public float Temperature { get; private set; }
    public float Humidity { get; private set; }
    /// <summary>Atmospheric pressure in hectopascals.</summary>
    public float Pressure { get; private set; }

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
    /// <summary>
    /// Raised on every report of the pressure, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Pressure"/>
    /// </summary>
    public event Func<float, Task> OnPressureChangeAsync = _ => Task.CompletedTask;

    public AqaraTemperatureHumiditySensor(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("temperature", value => UpdateTemperature(int.Parse(value.ReadString()) / 100f));
        map.XiaomiGateway2("humidity", value => UpdateHumidity(int.Parse(value.ReadString()) / 100f));
        map.XiaomiGateway2("pressure", value => UpdatePressure(int.Parse(value.ReadString()) / 10f));

        map.LumiMqtt("0.3.85", "pressure",
            value => UpdatePressure(value.ReadInt32() / 100f),
            value => Pressure = value.GetValue<int>() / 100f);
        map.LumiMqtt("0.1.85", "temperature",
            value => UpdateTemperature(value.ReadInt32() / 100f),
            value => Temperature = value.GetValue<int>() / 100f);
        map.LumiMqtt("0.2.85", "humidity",
            value => UpdateHumidity(value.ReadInt32() / 100f),
            value => Humidity = value.GetValue<int>() / 100f);
        MapBattery(map);
    }

    internal void UpdateTemperature(float value)
    {
        SetAndRaise(nameof(OnTemperatureChangeAsync), Temperature, () => Temperature = value, OnTemperatureChangeAsync);
    }

    internal void UpdateHumidity(float value)
    {
        SetAndRaise(nameof(OnHumidityChangeAsync), Humidity, () => Humidity = value, OnHumidityChangeAsync);
    }

    internal void UpdatePressure(float value)
    {
        SetAndRaise(nameof(OnPressureChangeAsync), Pressure, () => Pressure = value, OnPressureChangeAsync);
    }





    public override string ToString()
    {
        var model = Model ?? LUMI_MQTT_MODEL;
        return $"Device: {MARKET_MODEL} {model} {DeviceId}, Temperature: {Temperature}°C, Humidity: {Humidity}%, " +
        $"Pressure: {Pressure}hPa, Voltage: {Voltage}V, BatteryPercent: {BatteryPercent}%, " +
        $"LinkQuality: {LinkQuality}, ChipTemperature: {ChipTemperature}°C";
    }
}
