using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public class XiaomiThSensor : ZigBeeBatteryDevice
{
    public const string MARKET_MODEL = "WSDCGQ01LM";
    public const string MODEL = "lumi.sensor_ht";
    private const string TEMPERATURE_RES_NAME = "0.1.85";
    private const string HUMIDITY_RES_NAME = "0.2.85";
    public float Temperature { get; internal set; }
    public float Humidity { get; internal set; }    
    /// <summary>
    /// Old value temperature -40-125°C (0.1 step) passed as an argument
    /// </summary>
    public event Action<float> OnTemperatureChange;
    /// <summary>
    /// Old value relative humidity 0-100% (1 step) passed as an argument
    /// </summary>
    public event Action<float> OnHumidityChange;
    public XiaomiThSensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        ResNamesToActions.Add(TEMPERATURE_RES_NAME, x =>
        {
            var oldVal = Temperature;
            Temperature = (float)x.GetDouble()/100;
            OnTemperatureChange?.Invoke(oldVal);
        });

        ResNamesToActions.Add(HUMIDITY_RES_NAME, x =>
        {
            var oldVal = Humidity;
            Humidity = (float)x.GetDouble()/100;
            OnHumidityChange?.Invoke(oldVal);
        });
    }
    protected internal override string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["temperature", "humidity", .. base.GetProps()];
    }
    protected internal override void SetProps(JsonNode[] props)
    {
        Temperature = props[0].GetValue<int>()/100f;
        Humidity = props[1].GetValue<int>()/100f;
        base.SetProps(props.Skip(2).ToArray());
    }
    protected string GetBaseToString() => base.ToString();
    
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Temperature: {Temperature}°C, " +
            $"Humidity: {Humidity}%, " + base.ToString();
    }
}
