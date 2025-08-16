using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiWindowDoorSensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "MCCGQ01LM";
    public const string MODEL = "magnet";

    public XiaomiWindowDoorSensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("status", async x => 
        {
            var status = x.GetString();

            if (status == "open")
            {
                Status = status;
                await OnOpenAsync();
            }
            else if (status == "close")
            {
                Status = status;
                await OnCloseAsync();
            }
        });

        Actions.Add("no_close", async x => 
        {
            var noClose = int.Parse(x.GetString());

            if(noClose == 60 && NotClosedFor1MinuteAsync is not null) 
            {
                await NotClosedFor1MinuteAsync();
            }
            else if(noClose == 300 && NotClosedFor5MinutesAsync is not null) 
            {
                await NotClosedFor5MinutesAsync();
            }
        });
    }

    public event Func<Task> OnOpenAsync = () => Task.CompletedTask;

    public event Func<Task> OnCloseAsync = () => Task.CompletedTask;

    public event Func<Task> NotClosedFor1MinuteAsync = () => Task.CompletedTask;

    public event Func<Task> NotClosedFor5MinutesAsync = () => Task.CompletedTask;

    public string Status { get; private set; }

    public override string ToString() => base.ToString() + $", Status: {Status}";
}