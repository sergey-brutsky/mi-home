using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class Switch : MiHomeDevice<Switch>
    {
        public static string IdString => "switch";

        public override string Type => IdString;

        public event EventHandler OnClick;
        public event EventHandler OnDoubleClick;
        public event EventHandler OnLongPress;

        public Switch(string sid) : base(sid) { }

        public float? Voltage { get; set; }

        public string Status { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);
            var hasChanges = false;

            if (jObject["status"] != null)
            {
                hasChanges |= ChangeAndDetectChanges(() => Status, jObject["status"].ToString());

                if (Status == "click")
                    OnClick?.Invoke(this, EventArgs.Empty);

                if (Status == "double_click")
                    OnDoubleClick?.Invoke(this, EventArgs.Empty);

                if (Status == "long_click_press")
                    OnLongPress?.Invoke(this, EventArgs.Empty);
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out var v))
                Voltage = v / 1000;

            if (hasChanges)
                _changes.OnNext(this);
        }

        public override string ToString()
        {
            return $"Last status: {Status}, Voltage: {Voltage}V";
        }
    }
}