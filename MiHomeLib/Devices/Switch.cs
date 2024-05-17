using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class Switch : MiHomeDevice
    {
        public const string TypeKey = "switch";

        public event EventHandler OnClick;

        public event EventHandler OnDoubleClick;

        public event EventHandler OnLongPress;

        public Switch(string sid) : base(sid, TypeKey) {}

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

            Voltage = jObject.ParseVoltage() ?? Voltage;
        }

        public override string ToString()
        {
            return $"Last status: {Status}, Voltage: {Voltage}V";
        }
    }
}