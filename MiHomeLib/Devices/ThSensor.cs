using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class ThSensor : MiHomeDevice
    {
        public event EventHandler<TemperatureEventArgs> OnTemperatureChange;
        public event EventHandler<HumidityEventArgs> OnHumidityChange;

        public ThSensor(string sid) : base(sid, "sensor_ht") {}

        public float? Voltage { get; private set; }
        public float? Temperature { get; private set; }
        public float? Humidity { get; private set;  }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["temperature"] != null && float.TryParse(jObject["temperature"].ToString(), out float t))
            {
                var newTemperature = t / 100;

                if (Temperature != null && Math.Abs(newTemperature - Temperature.Value) > 0.01)
                {
                    OnTemperatureChange?.Invoke(this, new TemperatureEventArgs(newTemperature));
                }

                Temperature = newTemperature;
            }

            if (jObject["humidity"] != null && float.TryParse(jObject["humidity"].ToString(), out float h))
            {
                var newHumidity = h / 100;

                if (Humidity != null && Math.Abs(newHumidity- Humidity.Value) > 0.01)
                {
                    OnHumidityChange?.Invoke(this, new HumidityEventArgs(newHumidity));
                }

                Humidity = newHumidity;
            }
            
            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float v))
            {
                Voltage = v / 1000;
            }
        }

        public override string ToString()
        {
            return $"{(!string.IsNullOrEmpty(Name) ? "Name: "+ Name +", " : string.Empty)}Temperature: {Temperature}°C, Humidity: {Humidity}%, Voltage: {Voltage}V";
        }
    }
}