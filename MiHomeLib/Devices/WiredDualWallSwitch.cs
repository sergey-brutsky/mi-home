using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class WiredDualWallSwitch : MiHomeDevice<WiredDualWallSwitch>
    {
        public static string IdString => "ctrl_neutral2";

        public override string Type => IdString;

        public event EventHandler OnSwitchChannelRight;
        public event EventHandler OnSwitchChannelLeft;

        public WiredDualWallSwitch(string sid) : base(sid) 
        {
            StatusLeft = "idle";
            StatusRight = "idle";
        }

        public string StatusLeft { get; private set; }
        public string StatusRight { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);
            var hasChanges = false;

            if (jObject["channel_0"] != null)
            {
                hasChanges |= ChangeAndDetectChanges(() => StatusLeft, jObject["channel_0"].ToString());
                OnSwitchChannelLeft?.Invoke(this, new WallSwitchEventArgs(StatusLeft));
            }
            if (jObject["channel_1"] != null)
            {
                hasChanges |= ChangeAndDetectChanges(() => StatusRight, jObject["channel_1"].ToString());
                OnSwitchChannelRight?.Invoke(this, new WallSwitchEventArgs(StatusRight));
            }

            if (hasChanges)
                _changes.OnNext(this);
        }
        
        public override string ToString()
        {
            return $"Status Left: {StatusLeft}, Right: {StatusRight}";
        }
    }
}