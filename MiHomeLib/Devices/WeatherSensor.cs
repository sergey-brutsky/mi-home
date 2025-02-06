using MiHomeLib.Events;
using System;
using System.Text.Json.Nodes;

namespace MiHomeLib.Devices
{
    public class WeatherSensor(string sid) : ThSensor(sid, TypeKey)
    {
        public new const string TypeKey = "weather.v1";
        
        public event EventHandler<PressureEventArgs> OnPressureChange;

        public float? Pressure { get; private set; }

        public override void ParseData(string command)
        {
            base.ParseData(command);

            var jObject = JsonNode.Parse(command).AsObject();

            if (jObject["pressure"] == null || !float.TryParse(jObject["pressure"].ToString(), out float p)) return;

            var newPressure = p / 100;

            if (Pressure == null || Pressure != null && Math.Abs(newPressure - Pressure.Value) > 0.01)
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
