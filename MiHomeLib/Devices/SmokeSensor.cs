using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SmokeSensor : MiHomeDevice<SmokeSensor>
    {
        public static string IdString => "smoke";

        public override string Type => IdString;

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
            var hasChanges = false;

            if (jObject["alarm"] != null && bool.TryParse(jObject["alarm"].ToString(), out var alarm))
            {
                hasChanges |= Alarm != alarm;
                if (alarm)
                    OnAlarm?.Invoke(this, new EventArgs());
                else
                    OnAlarmStopped?.Invoke(this, new EventArgs());
                
                Alarm = alarm;
            }

            if (jObject["density"] != null && float.TryParse(jObject["density"].ToString(), out var h))
            {
                var newDensity = h / 100;

                if (Density != null && Math.Abs(newDensity - Density.Value) > 0.01)
                {
                    hasChanges = true;
                    OnDensityChange?.Invoke(this, new DensityEventArgs(newDensity));
                }

                Density = newDensity;
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out var v))
                Voltage = v / 1000;

            if (hasChanges)
                _changes.OnNext(this);
        }

        public override string ToString()
        {
            return $"{(!string.IsNullOrEmpty(Name) ? "Name: " + Name + ", " : string.Empty)}Alarm: {(Alarm ? "on" : "off")}, Density: {Density ?? 0}, Voltage: {Voltage}V";
        }
    }
}