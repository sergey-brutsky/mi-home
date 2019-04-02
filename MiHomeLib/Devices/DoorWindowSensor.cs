using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class DoorWindowSensor : MiHomeDevice<DoorWindowSensor>
    {
        public static string IdString => "magnet" +
                                         "";
        public override string Type => IdString;

        public event EventHandler OnOpen;
        public event EventHandler OnClose;

        public DoorWindowSensor(string sid) : base(sid) { }

        public float? Voltage { get; set; }

        public string Status { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);
            var hasChanges = false;

            if (jObject["status"] != null)
            {
                hasChanges = ChangeAndDetectChanges(() => Status, jObject["status"].ToString());
                if (hasChanges)
                {
                    if (Status == "open")
                        OnOpen?.Invoke(this, EventArgs.Empty);
                    else if (Status == "close")
                        OnClose?.Invoke(this, EventArgs.Empty);
                }
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