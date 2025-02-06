using System;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class AqaraOpenCloseSensor(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "sensor_magnet.aq2";

    public event EventHandler OnOpen;

    public event EventHandler OnClose;

    public float? Voltage { get; set; }

    public string Status { get; private set; }

    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();
        
        if (jObject.ParseString("status", out string status))
        {
            Status = status;

            if (Status == "open")
            {
                OnOpen?.Invoke(this, EventArgs.Empty);
            }
            else if (Status == "close")
            {
                OnClose?.Invoke(this, EventArgs.Empty);
            }
        }

        Voltage = jObject.ParseVoltage();
    }

    public override string ToString()
    {
        return $"{nameof(Voltage)}: {Voltage}V, {nameof(Status)}: {Status}";
    }
}