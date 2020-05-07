using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SmokeSensor : MiHomeDevice
    {
        public const string SensorKey = "smoke";

        public override string Type => SensorKey;
        public event EventHandler<DensityEventArgs> OnDensityChange;
        public event EventHandler<EventArgs> OnAlarm;
        public event EventHandler<EventArgs> OnAlarmStopped;

        public SmokeSensor(string sid) : base(sid) { }

        public float? Voltage { get; private set; }
        public bool Alarm { get; private set; }
        public float? Density { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["alarm"] != null && bool.TryParse(jObject["alarm"].ToString(), out bool alarm))
            {
                if (alarm)
                {
                    OnAlarm?.Invoke(this, new EventArgs());
                }
                else
                {
                    OnAlarmStopped?.Invoke(this, new EventArgs());
                }
                
                Alarm = alarm;
            }

            if (jObject["density"] != null && float.TryParse(jObject["density"].ToString(), out float h))
            {
                var newDensity = h / 100;

                if (Density != null && Math.Abs(newDensity - Density.Value) > 0.01)
                {
                    OnDensityChange?.Invoke(this, new DensityEventArgs(newDensity));
                }

                Density = newDensity;
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float v))
            {
                Voltage = v / 1000;
            }
        }

        public override string ToString()
        {
            return $"{(!string.IsNullOrEmpty(Name) ? "Name: " + Name + ", " : string.Empty)}Alarm: {(Alarm ? "on" : "off")}, Density: {Density ?? 0}, Voltage: {Voltage}V";
        }
    }
}