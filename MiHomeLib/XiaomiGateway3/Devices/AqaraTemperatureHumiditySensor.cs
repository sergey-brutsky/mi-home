using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;

// This sensor is exactly like XiaomiThSensor but also has pressure measurement support
public class AqaraTemperatureHumiditySensor : XiaomiTemperatureHumiditySensor
{
    public new const string MARKET_MODEL = "WSDCGQ11LM";
    public new const string MODEL = "lumi.weather";
    private const string PRESSURE_RES_NAME = "0.3.85";
    public float Pressure { get; internal set; } 
    /// <summary>
    /// Old value pressure (0.1 step) passed as an argument
    /// </summary>
    public event Func<float, Task> OnPressureChangeAsync = (_) => Task.CompletedTask;
    public AqaraTemperatureHumiditySensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Actions.Add(PRESSURE_RES_NAME, async x =>
        {
            var oldVal = Pressure;
            Pressure = (float)x.GetDouble()/100;
            await OnPressureChangeAsync(oldVal);
        });
    }
    protected internal override string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["pressure", .. base.GetProps()];
    }
    protected internal override void SetProps(JsonNode[] props)
    {
        Pressure = props[0].GetValue<int>()/100f;
        base.SetProps(props.Skip(1).ToArray());
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Temperature: {Temperature}°C, " +
            $"Humidity: {Humidity}%, " + 
            $"Pressure: {Pressure}hPa, "  + GetBaseToString();
    }
}
