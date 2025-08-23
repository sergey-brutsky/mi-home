using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

public class XiaomiBluetoothHygrothermograph2 : BleBatteryDevice
{
    public const string MARKET_MODEL = "LYWSD03MMC";
    public const string MODEL = "miaomiaoce.sensor_ht";
    public const int PDID = 1371;
    private const int TEMPERATURE_EID = 4100;
    private const int HUMIDITY_EID = 4102;

    public XiaomiBluetoothHygrothermograph2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(TEMPERATURE_EID, async x => 
        {
            var oldTemperature = Temperature;
            Temperature = ToBleFloat(x);
            await OnTemperatureChangeAsync(oldTemperature);
        });

        EidToActions.Add(HUMIDITY_EID, async x => 
        {
            var oldHumidity = Humidity;
            Humidity = ToBleFloat(x);
            await OnHumidityChangeAsync(oldHumidity);
        });
    }
    public float Temperature { get; set; }
    /// <summary>
    /// Old value temperature -30 - 100°C (0.1 step) passed as an argument
    /// </summary>
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;
    public float Humidity { get; set; }
    /// <summary>
    /// Old value relative humidity 0-100% (0.1 step) passed as an argument
    /// </summary>
    public event Func<float, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
                $"Temperature: {Temperature}°C, " +
                $"Humidity: {Humidity}%, " +
                $"Battery Percent: {BatteryPercent}% ";
    }
}
