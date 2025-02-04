using System;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class Switch(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "switch";

    public event EventHandler OnClick;

    public event EventHandler OnDoubleClick;

    public event EventHandler OnLongPress;

    public float? Voltage { get; set; }

    public string Status { get; private set; }

    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();

        if (jObject.ParseString("status", out string status))
        {
            Status = status;

            if (Status == "click")
            {
                OnClick?.Invoke(this, EventArgs.Empty);
            } 
            else if (Status == "double_click")
            {
                OnDoubleClick?.Invoke(this, EventArgs.Empty);
            }
            else if (Status == "long_click_press")
            {
                OnLongPress?.Invoke(this, EventArgs.Empty);
            }
        }

        Voltage = jObject.ParseVoltage();
    }

    public override string ToString() => $"Last status: {Status}, Voltage: {Voltage}V";
}