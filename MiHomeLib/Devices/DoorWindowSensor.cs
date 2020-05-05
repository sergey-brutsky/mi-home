using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class DoorWindowSensor : MiHomeDevice
    {
        public const string SensorKey = "magnet";

        public override string Type => SensorKey;

        public event EventHandler OnOpen;

        public event EventHandler OnClose;

        public DoorWindowSensor(string sid) : base(sid) { }

        public float? Voltage { get; set; }

        public string Status { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {
                Status = jObject["status"].ToString();

                if (Status == "open")
                {
                    OnOpen?.Invoke(this, EventArgs.Empty);
                }
                else if (Status == "close")
                {
                    OnClose?.Invoke(this, EventArgs.Empty);
                }
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float v))
            {
                Voltage = v / 1000;
            }
        }
        public override string ToString()
        {
            return $"Status: {Status}, Voltage: {Voltage}V";
        }
    }
}