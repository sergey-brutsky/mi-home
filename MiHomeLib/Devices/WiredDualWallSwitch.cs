using System;
using System.Text.Json.Nodes;
using MiHomeLib.Events;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class WiredDualWallSwitch(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "ctrl_neutral2";

    public event EventHandler OnSwitchChannelRight;

    public event EventHandler OnSwitchChannelLeft;

    public string StatusLeft { get; private set; } = "idle";
    public string StatusRight { get; private set; } = "idle";

    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();

        if (jObject.ParseString("channel_0", out string channel0))
        {
            StatusLeft = channel0;
            OnSwitchChannelLeft?.Invoke(this, new WallSwitchEventArgs(StatusLeft));
        }
        
        if (jObject.ParseString("channel_1", out string channel1))
        {
            StatusRight = channel1;
            OnSwitchChannelRight?.Invoke(this, new WallSwitchEventArgs(StatusRight));
        } 
    }

    public override string ToString() => $"Status Left: {StatusLeft}, Right: {StatusRight}";
}