using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraVirationSensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "DJT11LM";
    public const string MODEL = "vibration";
    
    public AqaraVirationSensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", async x => 
        {
            switch (x.GetString())
            {
                case "vibrate":
                    await OnVibrationAsync();
                    break;
                case "tilt":
                    await OnTiltAsync();
                    break;
                case "free_fall":
                    await OnFreeFallAsync();
                    break;
            }
        });  

        Actions.Add("final_tilt_angle", async x => 
        {
            await OnFinalTiltAngleAsync(int.Parse(x.GetString()));
        }); 

        Actions.Add("coordination", async x => 
        {
            var coords = x.GetString().Split(',').Select(int.Parse).ToArray();
            await OnCoordinationsAsync((coords[0], coords[1], coords[2]));
        });  

        Actions.Add("bed_activity", async x => 
        {
            await OnBedActivityAsync(int.Parse(x.GetString()));
        }); 
    }
    public event Func<Task> OnVibrationAsync = () => Task.CompletedTask;
    public event Func<Task> OnTiltAsync = () => Task.CompletedTask;
    public event Func<Task> OnFreeFallAsync = () => Task.CompletedTask;
    public event Func<int, Task> OnFinalTiltAngleAsync = (_) => Task.CompletedTask;
    public event Func<(int X, int Y, int Z), Task> OnCoordinationsAsync = (_) => Task.CompletedTask;
    public event Func<int, Task> OnBedActivityAsync;
}
