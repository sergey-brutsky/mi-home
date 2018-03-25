using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class WiredDualWallSwitch : MiHomeDevice
    {
        public event EventHandler OnSwitchChannelRight;
        public event EventHandler OnSwitchChannelLeft;

        public WiredDualWallSwitch(string sid) : base(sid, "ctrl_neutral2") 
        {
            StatusLeft = "idle";
            StatusRight = "idle";
        }

        public float? Voltage { get; set; }

        public string StatusLeft { get; private set; }
        public string StatusRight { get; private set; }

        public override void ParseData(string command)
        {
            Console.WriteLine(command);
            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {

            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float v))
            {
                Voltage = v / 1000;
            }
        }
        public override string ToString()
        {
            return $"Status Left: {StatusLeft}, Right: {StatusRight}, Voltage: {Voltage}V";
        }
    }
}