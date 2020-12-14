using MiHomeLib.Events;
using Newtonsoft.Json.Linq;
using System;

namespace MiHomeLib.Devices
{
    public class WeatherSensor : ThSensor
    {
        public new const string TypeKey = "weather.v1";
        
        public event EventHandler<PressureEventArgs> OnPressureChange;
        
        public WeatherSensor(string sid) : base(sid, TypeKey) {}

        public float? Pressure { get; private set; }

        public override void ParseData(string command)
        {
            base.ParseData(command);

            var jObject = JObject.Parse(command);

            if (jObject["pressure"] == null || !float.TryParse(jObject["pressure"].ToString(), out float p)) return;

            var newPressure = p / 100;

            if (Pressure == null || Temperature != null && Math.Abs(newPressure - Pressure.Value) > 0.01)
            {
                OnPressureChange?.Invoke(this, new PressureEventArgs(newPressure));
            }

            Pressure = newPressure;
        }

        public override string ToString()
        {
            return $"Temperature: {Temperature}°C, Humidity: {Humidity}%, Pressure: {Pressure}kPa, Voltage: {Voltage}V";
        }
    }
}
