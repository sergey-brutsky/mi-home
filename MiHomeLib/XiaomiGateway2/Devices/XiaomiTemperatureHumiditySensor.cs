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
        Actions.Add("temperature", async x => 
        {
            Temperature = int.Parse(x.GetString())/100f;
            await OnTemperatureChangeAsync(Temperature);
        });

        Actions.Add("humidity", async x => 
        {
            Humidity = int.Parse(x.GetString())/100f;
            await OnHumidityChangeAsync(Humidity);
        }); 
    }
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;
    public event Func<float, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;
    public float Temperature { get; private set; }
    public float Humidity { get; private set;  }
    public override string ToString() => base.ToString() + $", Temperature: {Temperature}°C, Humidity: {Humidity}%";
}


