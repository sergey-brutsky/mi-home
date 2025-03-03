using System;
using System.Text.Json.Nodes;
using MiHomeLib.Events;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class AqaraMotionSensor(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "sensor_motion.aq2";
    
    public event EventHandler OnMotion;

    public event EventHandler<NoMotionEventArgs> OnNoMotion;

    public float? Voltage { get; set; }

    public string Status { get; private set; }

    public int Lux { get; private set; }

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

        if (jObject.ParseInt("lux", out int lux))
        {
            Lux = lux;
        }

        if (jObject.ParseInt("no_motion", out int noMotion))
        {
            NoMotion = noMotion;
            OnNoMotion?.Invoke(this, new NoMotionEventArgs(NoMotion));
        }

        Voltage = jObject.ParseVoltage();
    }

    public DateTime? MotionDate { get; private set; }

    public override string ToString()
    {
        return $"{nameof(Voltage)}: {Voltage}V, {nameof(Status)}: {Status}, {nameof(Lux)}:{Lux}, {nameof(NoMotion)}: {NoMotion}s";
    }
}