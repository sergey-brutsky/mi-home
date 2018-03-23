using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class Switch : MiHomeDevice
    {
        public event EventHandler OnClick;
        public event EventHandler OnDoubleClick;
        public event EventHandler OnLongPress;

        public Switch(string sid) : base(sid, "switch") { }

        public float? Voltage { get; set; }

        public string Status { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {
                Status = jObject["status"].ToString();

                if (Status == "click")
                {
                    OnClick?.Invoke(this, EventArgs.Empty);
                }

                if (Status == "double_click")
                {
                    OnDoubleClick?.Invoke(this, EventArgs.Empty);
                }

                if (Status == "long_click_press")
                {
                    OnLongPress?.Invoke(this, EventArgs.Empty);
                }
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float v))
            {
                Voltage = v / 1000;
            }
        }

        public override string ToString()
        {
            return $"Last status: {Status}, Voltage: {Voltage}V";
        }
    }
}