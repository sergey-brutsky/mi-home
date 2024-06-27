using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

// This sensor is exactly like XiaomiThSensor but also has pressure measurement support
public class AqaraThSensor : XiaomiThSensor
{
    public new const string MARKET_MODEL = "WSDCGQ11LM";
    public new const string MODEL = "lumi.weather";
    private const string PRESSURE_RES_NAME = "0.3.85";
    public float Pressure { get; internal set; } 
    /// <summary>
    /// Old value pressure (0.1 step) passed as an argument
    /// </summary>
    public event Action<float> OnPressureChange;
    public AqaraThSensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        ResNamesToActions.Add(PRESSURE_RES_NAME, x =>
        {
            var oldVal = Pressure;
            Pressure = (float)x.GetDouble()/100;
            OnPressureChange?.Invoke(oldVal);
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
