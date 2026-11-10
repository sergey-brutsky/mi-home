using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraTemperatureHumiditySensor : XiaomiTemperatureHumiditySensor
{
    public new const string MARKET_MODEL = "WSDCGQ11LM";
    public new const string MODEL = "weather.v1";

    public AqaraTemperatureHumiditySensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("pressure", x => 
        {
            Pressure = int.Parse(x.GetString())/100f;
            
            RaiseEvent(() => OnPressureChangeAsync(Pressure));
        });
    }

    public event Func<float, Task> OnPressureChangeAsync = (_) => Task.CompletedTask;

    public float Pressure { get; private set; }

    public override string ToString() => base.ToString() + $", Pressure: {Pressure}kPa";
}
