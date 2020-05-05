using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class AqaraOpenCloseSensor : MiHomeDevice
    {
        public const string SensorKey = "sensor_magnet.aq2";

        public override string Type => SensorKey;

        public event EventHandler OnOpen;

        public event EventHandler OnClose;

        public AqaraOpenCloseSensor(string sid) : base(sid)
        {
        }

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

            Debug.WriteLine($"Sid: {Sid}, Type: {GetType().Name}, Command: {command}, Sensor: {this}");
        }

        public override string ToString()
        {
            return $"{nameof(Voltage)}: {Voltage}V, {nameof(Status)}: {Status}";
        }
    }
}