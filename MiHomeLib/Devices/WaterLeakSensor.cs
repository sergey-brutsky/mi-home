using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class WaterLeakSensor : MiHomeDevice
    {
        public event EventHandler OnLeak;
        public event EventHandler OnNoLeak;
        
        public WaterLeakSensor(string sid) : base(sid, "sensor_wleak.aq1") { }

        public string Status { get; private set; }

        public float? Voltage { get; set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {
                Status = jObject["status"].ToString();

                if (Status == "leak")
                {
                    OnLeak?.Invoke(this, EventArgs.Empty);
                }
                else if (Status == "no_leak")
                {
                    OnNoLeak?.Invoke(this, EventArgs.Empty);
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