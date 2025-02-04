using System;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class WirelessDualWallSwitch(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "remote.b286acn01";
    private const string LeftChannel = "channel_0";
    private const string RightChannel = "channel_1";

    public event Action<EventArgs> OnRightClick;
    public event Action<EventArgs> OnLeftClick;
    public event Action<EventArgs> OnRightDoubleClick;
    public event Action<EventArgs> OnLeftDoubleClick;
    public event Action<EventArgs> OnRightLongClick;
    public event Action<EventArgs> OnLeftLongClick;
    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();

        if (jObject.ParseString(LeftChannel, out string leftChannel))
        {
            if (leftChannel == "click")
            {
                OnLeftClick?.Invoke(new EventArgs());
            }
            else if (leftChannel == "double_click")
            {
                OnLeftDoubleClick?.Invoke(new EventArgs());
            }
            else if (leftChannel == "long_click")
            {
                OnLeftLongClick?.Invoke(new EventArgs());
            }
        }

        if (jObject.ParseString(RightChannel, out string rightChannel))
        {
            if (rightChannel == "click")
            {
                OnRightClick?.Invoke(new EventArgs());
            }
            else if (rightChannel == "double_click")
            {
                OnRightDoubleClick?.Invoke(new EventArgs());
            }
            else if (rightChannel == "long_click")
            {
                OnRightLongClick?.Invoke(new EventArgs());
            }
        }
    }
}