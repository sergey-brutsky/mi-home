using MiHomeLib.Protocols;
// Support for this device has been partially implemented on top of https://home.miot-spec.com/spec/miaomiaoce.sensor_ht.t9
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// Xiaomi TH Sensor 3 (MJWSD05MMC)
/// </summary>
[ProtocolDeviceModel(DeviceProtocol.LumiBle, MODEL, PDID)]
public sealed class XiaomiBluetoothHygrothermograph3 : ProtocolMappedDeviceBase, IProtocolMappedBleDevice, ITemperatureSensor, IHumiditySensor, IBatteryPowered
{
    public const string MARKET_MODEL = "MJWSD05MMC";
    public const string MODEL = "miaomiaoce.sensor_ht.t9";
    public const int PDID = 10290;
    private const int SENSOR_SIID = 3;
    private const int TEMPERATURE_PIID = 1001;
    private const int HUMIDITY_PIID = 1002;
    private const int BATTERY_SIID = 2;
    private const int BATTERY_PIID = 1003;

    private bool _hasTemperatureReport;
    private bool _hasHumidityReport;
    private bool _hasBatteryReport;

    public override string DeviceId { get; }
    public string Mac { get; private set; }

    public XiaomiBluetoothHygrothermograph3(string deviceId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
    }

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiBleMiSpec(BATTERY_SIID, BATTERY_PIID, value =>
        {
            var oldBatteryPercent = BatteryPercent;

            if (byte.TryParse(value.ToString(), out var result))
            {
                BatteryPercent = result;
                _hasBatteryReport = true;
                RaiseEvent(nameof(OnBatteryPercentChangeAsync), OnBatteryPercentChangeAsync, oldBatteryPercent);
            }
        });

        map.LumiBleMiSpec(SENSOR_SIID, TEMPERATURE_PIID, value =>
        {
            var oldTemperature = Temperature;

            if (float.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                Temperature = result;
                _hasTemperatureReport = true;
                RaiseEvent(nameof(OnTemperatureChangeAsync), OnTemperatureChangeAsync, oldTemperature);
            }
        });

        map.LumiBleMiSpec(SENSOR_SIID, HUMIDITY_PIID, value =>
        {
            var oldHumidity = Humidity;

            if (byte.TryParse(value.ToString(), out var result))
            {
                Humidity = result;
                _hasHumidityReport = true;
                RaiseEvent(nameof(OnHumidityChangeAsync), OnHumidityChangeAsync, oldHumidity);
            }
        });
    }

    public float Temperature { get; private set; }
    public byte Humidity { get; private set; }
    public byte BatteryPercent { get; private set; }
    float IHumiditySensor.Humidity => Humidity;

    /// <summary>
    /// Raised on every report, including unchanged values. The previous value is passed as an argument; the new value is in the property.
    /// </summary>
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Raised on every report, including unchanged values. The previous value is passed as an argument; the new value is in the property.
    /// </summary>
    public event Func<byte, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Raised on every report, including unchanged values. The previous value is passed as an argument; the new value is in the property.
    /// </summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = (_) => Task.CompletedTask;

    public override string ToString()
    {
        var temperature = _hasTemperatureReport ? $"{Temperature}°C" : "Unknown";
        var humidity = _hasHumidityReport ? $"{Humidity}%" : "Unknown";
        var battery = _hasBatteryReport ? $"{BatteryPercent}%" : "Unknown";
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}, " +
               $"Temperature: {temperature}, Humidity: {humidity}, Battery Percent: {battery}";
    }
}
