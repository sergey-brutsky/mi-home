using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiHoneywellSmokeDetector : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "JTYJ-GD-01LM/BW";
    public const string MODEL = "smoke";

    public XiaomiHoneywellSmokeDetector(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("alarm", async x => 
        {
            Alarm = int.Parse(x.GetString()) == 1;

            if (Alarm)
            {
                await OnAlarmAsync();
            }
            else
            {
                await OnAlarmStoppedAsync();
            }
        });

        Actions.Add("density", async x => 
        {
            await OnDensityChangeAsync(int.Parse(x.ToString()) / 100f);
        });
    }

    public event Func<float, Task> OnDensityChangeAsync = (_) => Task.CompletedTask;
    public event Func<Task> OnAlarmAsync = () => Task.CompletedTask;
    public event Func<Task> OnAlarmStoppedAsync = () => Task.CompletedTask;
    public bool Alarm { get; private set; }
    public override string ToString() => base.ToString() + $", Alarm: {Alarm}";
}