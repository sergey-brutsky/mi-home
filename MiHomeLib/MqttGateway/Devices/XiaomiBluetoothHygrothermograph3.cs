// Support for this device has been implemented on top of https://home.miot-spec.com/spec/miaomiaoce.sensor_ht.t9
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// Xiaomi TH Sensor 3 (MJWSD05MMC).
/// Unlike LYWSD03MMC this device reports data via mibeacon v2 events.
/// </summary>
public class XiaomiBluetoothHygrothermograph3 : BleBatteryDevice
{
    public const string MARKET_MODEL = "MJWSD05MMC";
    public const string MODEL = "miaomiaoce.sensor_ht.t9";
    public const int PDID = 10290;
    private const int TEMPERATURE_EID = 19457;
    private const int HUMIDITY_EID = 19458;
    private const int BATTERY_MIBEACON2_EID = 18435;

    public XiaomiBluetoothHygrothermograph3(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory, BATTERY_MIBEACON2_EID)
    {
        EidToActions.Add(TEMPERATURE_EID, async x =>
        {
            var oldTemperature = Temperature;
            Temperature = (float)Math.Round(ToBleFloat32(x), 1);
            await OnTemperatureChangeAsync(oldTemperature);
        });

        EidToActions.Add(HUMIDITY_EID, async x =>
        {
            var oldHumidity = Humidity;
            Humidity = ToBleByte(x);
            await OnHumidityChangeAsync(oldHumidity);
        });
    }

    public float Temperature { get; set; }
    /// <summary>
    /// Old value temperature -30 - 100°C (0.1 step) passed as an argument
    /// </summary>
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;

    public byte Humidity { get; set; }
    /// <summary>
    /// Old value relative humidity 0-100% (1% step) passed as an argument
    /// </summary>
    public event Func<byte, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;

    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
                $"Temperature: {Temperature}°C, " +
                $"Humidity: {Humidity}%, " +
                $"Battery Percent: {BatteryPercent}% ";
    }
}
