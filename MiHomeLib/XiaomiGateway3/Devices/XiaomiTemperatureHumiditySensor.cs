using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;

public class XiaomiTemperatureHumiditySensor : ZigBeeBatteryDevice
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
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;
    /// <summary>
    /// Old value relative humidity 0-100% (1 step) passed as an argument
    /// </summary>
    public event Func<float, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;
    public XiaomiTemperatureHumiditySensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Actions.Add(TEMPERATURE_RES_NAME, async x =>
        {
            var oldVal = Temperature;
            Temperature = (float)x.GetDouble()/100;
            await OnTemperatureChangeAsync(oldVal);
        });

        Actions.Add(HUMIDITY_RES_NAME, async x =>
        {
            var oldVal = Humidity;
            Humidity = (float)x.GetDouble()/100;
            await OnHumidityChangeAsync(oldVal);
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
