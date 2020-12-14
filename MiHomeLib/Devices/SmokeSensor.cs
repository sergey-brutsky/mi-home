using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SmokeSensor : MiHomeDevice
    {
        public const string TypeKey = "smoke";

        public event EventHandler<DensityEventArgs> OnDensityChange;
        public event EventHandler<EventArgs> OnAlarm;
        public event EventHandler<EventArgs> OnAlarmStopped;

        public SmokeSensor(string sid) : base(sid, TypeKey) {}

        public float? Voltage { get; private set; }
        public bool Alarm { get; private set; }
        public float? Density { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if(jObject.ParseInt("alarm", out int alarm))
            {
                Alarm = alarm == 1;

                if (Alarm)
                {
                    OnAlarm?.Invoke(this, new EventArgs());
                }
                else
                {
                    OnAlarmStopped?.Invoke(this, new EventArgs());
                }
            }

            if (jObject.ParseFloat("density", out float h))
            {
                var newDensity = h / 100;

                if (Density != null && Math.Abs(newDensity - Density.Value) > 0.01)
                {
                    OnDensityChange?.Invoke(this, new DensityEventArgs(newDensity));
                }

                Density = newDensity;
            }

            Voltage = jObject.ParseVoltage();
        }

        public override string ToString()
        {
            return $"{(!string.IsNullOrEmpty(Name) ? "Name: " + Name + ", " : string.Empty)}Alarm: {(Alarm ? "on" : "off")}, Density: {Density ?? 0}, Voltage: {Voltage}V";
        }
    }
}