using System;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class WirelessDualWallSwitch : MiHomeDevice
    {
        private const string LeftChannel = "channel_0";
        private const string RightChannel = "channel_1";

        public event Action<EventArgs> OnRightClick;
        public event Action<EventArgs> OnLeftClick;

        public event Action<EventArgs> OnRightDoubleClick;
        public event Action<EventArgs> OnLeftDoubleClick;

        public event Action<EventArgs> OnRightLongClick;
        public event Action<EventArgs> OnLeftLongClick;

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject[LeftChannel] != null)
            {

                if (jObject[LeftChannel].Value<string>() == "click")
                {
                    OnLeftClick?.Invoke(new EventArgs());
                }
                else if (jObject[LeftChannel].Value<string>() == "double_click")
                {
                    OnLeftDoubleClick?.Invoke(new EventArgs());
                }
                else if (jObject[LeftChannel].Value<string>() == "long_click")
                {
                    OnLeftLongClick?.Invoke(new EventArgs());
                }

            }

            if (jObject[RightChannel] != null)
            {
                if (jObject[RightChannel].Value<string>() == "click")
                {
                    OnRightClick?.Invoke(new EventArgs());
                }
                else if (jObject[RightChannel].Value<string>() == "double_click")
                {
                    OnRightDoubleClick?.Invoke(new EventArgs());
                }
                else if (jObject[RightChannel].Value<string>() == "long_click")
                {
                    OnRightLongClick?.Invoke(new EventArgs());
                }
            }

        }

        public WirelessDualWallSwitch(string sid) : base(sid, "remote.b286acn01")
        {

        }
    }
}