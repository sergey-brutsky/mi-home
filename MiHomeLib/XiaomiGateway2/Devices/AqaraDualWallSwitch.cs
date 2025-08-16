using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraDualWallSwitch: XiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "QBKG03LM";
    public const string MODEL = "ctrl_neutral2";

    public AqaraDualWallSwitch(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("channel_0", async x => 
        {
            StatusLeft = x.GetString();
            await OnSwitchChannelLeftAsync(StatusLeft);
        });

        Actions.Add("channel_1", async x => 
        {
            StatusRight = x.GetString();
            await OnSwitchChannelRightAsync(StatusRight);
        });
    }

    public event Func<string, Task> OnSwitchChannelRightAsync = (_) => Task.CompletedTask;
    public event Func<string, Task> OnSwitchChannelLeftAsync = (_) => Task.CompletedTask;
    public string StatusLeft { get; private set; } = "idle";
    public string StatusRight { get; private set; } = "idle";
    public override string ToString() => base.ToString() + $", Left: {StatusLeft}, Right: {StatusRight}";
}