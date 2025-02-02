using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class AqaraOpenCloseSensor : MiHomeDevice
    {
        public const string TypeKey = "sensor_magnet.aq2";

        public event EventHandler OnOpen;

        public event EventHandler OnClose;

        public AqaraOpenCloseSensor(string sid) : base(sid, TypeKey) {}

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
            return $"{nameof(Voltage)}: {Voltage}V, {nameof(Status)}: {Status}";
        }
    }
}