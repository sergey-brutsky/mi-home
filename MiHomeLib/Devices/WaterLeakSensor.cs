using System;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class WaterLeakSensor(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "sensor_wleak.aq1";

    public event EventHandler OnLeak;
    public event EventHandler OnNoLeak;

    public string Status { get; private set; }

    public float? Voltage { get; set; }

    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();

        if (jObject.ParseString("status", out string status))
        {
            Status = status;

            if (Status == "leak")
            {
                OnLeak?.Invoke(this, EventArgs.Empty);
            }
            else if (Status == "no_leak")
            {
                OnNoLeak?.Invoke(this, EventArgs.Empty);
            }
        }

        Voltage = jObject.ParseVoltage();
    }

    public override string ToString() => $"Status: {Status}, Voltage: {Voltage}V";
}