using System;
using System.Text.Json.Nodes;
using MiHomeLib.Events;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class MotionSensor(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "motion";

    public event EventHandler OnMotion;
    public event EventHandler<NoMotionEventArgs> OnNoMotion;

    public float? Voltage { get; set; }

    public string Status { get; private set; }

    public int NoMotion { get; set; }

    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();

        if (jObject.ParseString("status", out string status))
        {
            Status = status;

            if (Status == "motion")
            {
                MotionDate = DateTime.Now;
                OnMotion?.Invoke(this, EventArgs.Empty);
            }
        }

        if (jObject.ParseInt("no_motion", out int noMotion))
        {
            Status = "no motion";
            NoMotion = noMotion;                
            OnNoMotion?.Invoke(this, new NoMotionEventArgs(NoMotion));
        }

        Voltage = jObject.ParseVoltage();
    }

    public DateTime? MotionDate { get; private set; }

    public override string ToString() => $"Status: {Status}, Voltage: {Voltage}V, NoMotion: {NoMotion}s";
}