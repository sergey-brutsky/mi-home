using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class AqaraMotionSensor : MiHomeDevice
    {
        public const string TypeKey = "sensor_motion.aq2";
        
        public event EventHandler OnMotion;

        public event EventHandler<NoMotionEventArgs> OnNoMotion;

        public AqaraMotionSensor(string sid) : base(sid, TypeKey) {}

        public float? Voltage { get; set; }

        public string Status { get; private set; }

        public int Lux { get; private set; }

        public int NoMotion { get; set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {
                Status = jObject["status"].ToString();

                if (Status == "motion")
                {
                    MotionDate = DateTime.Now;
                    OnMotion?.Invoke(this, EventArgs.Empty);
                }
            }

            if (jObject["lux"] != null)
            {
                Lux = int.Parse(jObject["lux"].ToString());
            }

            if (jObject["no_motion"] != null)
            {
                NoMotion = int.Parse(jObject["no_motion"].ToString());

                OnNoMotion?.Invoke(this, new NoMotionEventArgs(NoMotion));
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float v))
            {
                Voltage = v / 1000;
            }
        }

        public DateTime? MotionDate { get; private set; }

        public override string ToString()
        {
            return $"{nameof(Voltage)}: {Voltage}V, {nameof(Status)}: {Status}, {nameof(Lux)}:{Lux}, {nameof(NoMotion)}: {NoMotion}s";
        }
    }
}