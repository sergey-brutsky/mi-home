using System;
using Microsoft.Extensions.Logging;
using MiHomeLib.Utils;

namespace MiHomeLib.DevicesV3;

public class MiThMonitor2 : BleBatteryDevice
{
    public const string MARKET_MODEL = "LYWSD03MMC";
    public const string MODEL = "miaomiaoce.sensor_ht";
    public const int PDID = 1371;
    private const int TEMPERATURE_EID = 4100;
    private const int HUMIDITY_EID = 4102;

    public MiThMonitor2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(TEMPERATURE_EID, x => 
        {
            var oldTemperature = Temperature;
            Temperature = x.ToBleFloat();
            OnTemperatureChange?.Invoke(oldTemperature);
        });

        EidToActions.Add(HUMIDITY_EID, x => 
        {
            var oldHumidity = Humidity;
            Humidity = x.ToBleFloat();
            OnHumidityChange?.Invoke(oldHumidity);
        });
    }
    public float Temperature { get; set; }
    /// <summary>
    /// Old value temperature -30 - 100°C (0.1 step) passed as an argument
    /// </summary>
    public event Action<float> OnTemperatureChange;
    public float Humidity { get; set; }
    /// <summary>
    /// Old value relative humidity 0-100% (0.1 step) passed as an argument
    /// </summary>
    public event Action<float> OnHumidityChange;    
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
                $"Temperature: {Temperature}°C, " +
                $"Humidity: {Humidity}%, " +
                $"Battery Percent: {BatteryPercent}% ";
    }
}
