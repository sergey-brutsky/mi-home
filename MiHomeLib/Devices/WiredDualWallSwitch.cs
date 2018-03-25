using System;
using MiHomeLib.Events;
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

        public string StatusLeft { get; private set; }
        public string StatusRight { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["channel_0"] != null)
            {
                StatusLeft = jObject["channel_0"].ToString();
                OnSwitchChannelLeft?.Invoke(this, new WallSwitchEventArgs(StatusLeft));
            }
            if (jObject["channel_1"] != null)
            {
                StatusRight = jObject["channel_1"].ToString();
                OnSwitchChannelRight?.Invoke(this, new WallSwitchEventArgs(StatusRight));
            } 
        }
        
        public override string ToString()
        {
            return $"Status Left: {StatusLeft}, Right: {StatusRight}";
        }
    }
}