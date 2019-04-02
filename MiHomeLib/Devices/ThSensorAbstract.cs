using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public abstract class ThSensorAbstract<T> : MiHomeDevice<T> where T : ThSensorAbstract<T>, IMiHomeDevice, IMiHomeDevice<T>
    {
        public event EventHandler<TemperatureEventArgs> OnTemperatureChange;
        public event EventHandler<HumidityEventArgs> OnHumidityChange;

        protected ThSensorAbstract(string sid) : base(sid) { }

        public float? Voltage { get; private set; }
        public float? Temperature { get; private set; }
        public float? Humidity { get; private set;  }

        protected bool ParseDataInternal(string command)
        {
            var jObject = JObject.Parse(command);
            var hasChanges = false;

            if (jObject["temperature"] != null && float.TryParse(jObject["temperature"].ToString(), out var t))
            {
                var newTemperature = t / 100;

                if (Temperature != null && Math.Abs(newTemperature - Temperature.Value) > 0.01)
                {
                    hasChanges = true;
                    OnTemperatureChange?.Invoke(this, new TemperatureEventArgs(newTemperature));
                }

                Temperature = newTemperature;
            }

            if (jObject["humidity"] != null && float.TryParse(jObject["humidity"].ToString(), out var h))
            {
                var newHumidity = h / 100;

                if (Humidity != null && Math.Abs(newHumidity- Humidity.Value) > 0.01)
                {
                    hasChanges = true;
                    OnHumidityChange?.Invoke(this, new HumidityEventArgs(newHumidity));
                }

                Humidity = newHumidity;
            }
            
            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out var v))
            {
                Voltage = v / 1000;
            }

            return hasChanges;
        }

        public override string ToString()
        {
            return $"{(!string.IsNullOrEmpty(Name) ? "Name: "+ Name +", " : string.Empty)}Temperature: {Temperature}°C, Humidity: {Humidity}%, Voltage: {Voltage}V";
        }
    }
}