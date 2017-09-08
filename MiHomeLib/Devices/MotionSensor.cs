using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class MotionSensor : MiHomeDevice
    {
        public MotionSensor(string sid) : base(sid) { }

        public float? Voltage { get; set; }

        public string Status { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {
                Status = jObject["status"].ToString();
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