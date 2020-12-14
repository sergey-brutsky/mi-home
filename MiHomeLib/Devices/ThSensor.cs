using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class ThSensor : MiHomeDevice
    {
        public const string TypeKey = "sensor_ht";

        public event EventHandler<TemperatureEventArgs> OnTemperatureChange;

        public event EventHandler<HumidityEventArgs> OnHumidityChange;

        public ThSensor(string sid) : base(sid, TypeKey) {}
        public ThSensor(string sid, string TypeKey) : base(sid, TypeKey) { }

        public float? Voltage { get; private set; }
        public float? Temperature { get; private set; }
        public float? Humidity { get; private set;  }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if(jObject.ParseFloat("temperature", out float t))
            {
                var newTemperature = t / 100;

                if (Temperature != null && Math.Abs(newTemperature - Temperature.Value) > 0.01)
                {
                    OnTemperatureChange?.Invoke(this, new TemperatureEventArgs(newTemperature));
                }

                Temperature = newTemperature;
            }

            if (jObject.ParseFloat("humidity", out float h))
            {
                var newHumidity = h / 100;

                if (Humidity != null && Math.Abs(newHumidity - Humidity.Value) > 0.01)
                {
                    OnHumidityChange?.Invoke(this, new HumidityEventArgs(newHumidity));
                }

                Humidity = newHumidity;
            }

            Voltage = jObject.ParseVoltage();
        }

        public override string ToString()
        {
            return $"Temperature: {Temperature}°C, Humidity: {Humidity}%, Voltage: {Voltage}V";
        }
    }
}