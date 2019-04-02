using MiHomeLib.Events;
using Newtonsoft.Json.Linq;
using System;

namespace MiHomeLib.Devices
{
    public class WeatherSensor : ThSensorAbstract<WeatherSensor>
    {
        public static string IdString => "weather.v1";

        public override string Type => IdString;

        public event EventHandler<PressureEventArgs> OnPressureChange;
        
        public WeatherSensor(string sid) : base(sid) {}

        public float? Pressure { get; private set; }

        public override void ParseData(string command)
        {
            var hasChanges = base.ParseDataInternal(command);

            var jObject = JObject.Parse(command);

            if (jObject["pressure"] == null || !float.TryParse(jObject["pressure"].ToString(), out var p)) return;

            var newPressure = p / 100;

            if (Pressure == null || Temperature != null && Math.Abs(newPressure - Pressure.Value) > 0.01)
            {
                hasChanges = true;
                OnPressureChange?.Invoke(this, new PressureEventArgs(newPressure));
            }

            Pressure = newPressure;

            if (hasChanges)
                _changes.OnNext(this);
        }

        public override string ToString()
        {
            return $"{(!string.IsNullOrEmpty(Name) ? "Name: " + Name + ", " : string.Empty)}Temperature: {Temperature}°C, Humidity: {Humidity}%, Pressure: {Pressure}kPa, Voltage: {Voltage}V";
        }
    }
}
