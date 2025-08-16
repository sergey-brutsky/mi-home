using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraWindowDoorSensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "MCCGQ11LM";
    public const string MODEL = "sensor_magnet.aq2";

    public AqaraWindowDoorSensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", async x => 
        {
            var status = x.GetString();

            if (status == "open")
            {
                await OnOpenAsync();
            }
            else if (status == "close")
            {
                await OnCloseAsync();
            }
        });
    }

    public event Func<Task> OnOpenAsync = () => Task.CompletedTask;
    public event Func<Task> OnCloseAsync = () => Task.CompletedTask;
}