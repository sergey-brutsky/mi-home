using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiTemperatureHumiditySensor: BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "WSDCGQ01LM";
    public const string MODEL = "sensor_ht";

    public XiaomiTemperatureHumiditySensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("temperature", x => 
        {
            Temperature = int.Parse(x.GetString())/100f;
            RaiseEvent(() => OnTemperatureChangeAsync(Temperature));
        });

        Actions.Add("humidity", x => 
        {
            Humidity = int.Parse(x.GetString())/100f;
            RaiseEvent(() => OnHumidityChangeAsync(Humidity));
        }); 
    }
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;
    public event Func<float, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;
    public float Temperature { get; private set; }
    public float Humidity { get; private set;  }
    public override string ToString() => base.ToString() + $", Temperature: {Temperature}°C, Humidity: {Humidity}%";
}


