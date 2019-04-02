using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class WaterLeakSensor : MiHomeDevice<WaterLeakSensor>
    {
        public static string IdString => "sensor_wleak.aq1";

        public override string Type => IdString;

        public event EventHandler OnLeak;
        public event EventHandler OnNoLeak;

        public WaterLeakSensor(string sid) : base(sid) { }

        public string Status { get; private set; }

        public float? Voltage { get; set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);
            var hasChanges = false;

            if (jObject["status"] != null)
            {
                hasChanges |= ChangeAndDetectChanges(() => Status, jObject["status"].ToString());

                if (Status == "leak")
                    OnLeak?.Invoke(this, EventArgs.Empty);
                else if (Status == "no_leak")
                    OnNoLeak?.Invoke(this, EventArgs.Empty);
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out var v))
                Voltage = v / 1000;

            if (hasChanges)
                _changes.OnNext(this);
        }

        public override string ToString()
        {
            return $"Status: {Status}, Voltage: {Voltage}V";
        }
    }
}