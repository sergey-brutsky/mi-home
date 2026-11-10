using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraSmartHumanBodySensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "RTCGQ11LM";
    public const string MODEL = "sensor_motion.aq2";

    public AqaraSmartHumanBodySensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", x => 
        {
            Status = x.GetString();

            if (Status == "motion")
            {
                MotionDate = DateTime.Now;
                RaiseEvent(() => OnMotionAsync());
            }
        });

        Actions.Add("lux", x => 
        {
            Lux = x.GetInt32();
        });

        Actions.Add("no_motion", x => 
        {
            NoMotion = x.GetInt32();
            RaiseEvent(() => OnNoMotionAsync(NoMotion));
        });
    }

    public event Func<Task> OnMotionAsync = () => Task.CompletedTask;

    public event Func<int, Task> OnNoMotionAsync = (_) => Task.CompletedTask;
    
    public string Status { get; private set; }

    public int Lux { get; private set; }
    
    public int NoMotion { get; private set; }

    public DateTime? MotionDate { get; private set; }

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
}