using System;
using MiHomeLib.Commands;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class ThSensor : MiHomeDevice
    {
        public ThSensor(string sid) : base(sid) {}

        public float? Voltage { get; private set; }
        public float? Temperature { get; private set; }
        public float? Humidity { get; private set;  }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            float.TryParse(jObject["temperature"].ToString(), out float t);
            float.TryParse(jObject["humidity"].ToString(), out float h);
            float.TryParse(jObject["voltage"].ToString(), out float v);

            Temperature = t / 100;
            Humidity = h / 100;
            Voltage = v / 100;
        }
    }
}