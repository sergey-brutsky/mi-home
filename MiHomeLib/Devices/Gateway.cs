using System;
using MiHomeLib.Commands;
using Newtonsoft.Json.Linq;
#pragma warning disable CS0675

namespace MiHomeLib.Devices
{
    public class Gateway : MiHomeDevice
    {
        private readonly UdpTransport _transport;

        public Gateway(string sid, UdpTransport transport) : base(sid)
        {
            _transport = transport;
        }

        public string Rgb { get; private set; }
        public string Illumination { get; private set; }
        public string ProtoVersion { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject["rgb"] != null)
            {
                Rgb = jObject["rgb"].ToString();
            }

            if (jObject["illumination"] != null)
            {
                Illumination = jObject["illumination"].ToString();
            }

            if (jObject["proto_version"] != null)
            {
                ProtoVersion = jObject["proto_version"].ToString();
            }
        }

        public void EnableLight(byte r = 255, byte g = 255, byte b = 255, int illumination = 1000)
        {
            var rgb = 0xFF000000 | r << 16 | g << 8 | b; // found this trick from chinise gateway docs 

            if (illumination < 300 || illumination > 1300) throw new ArgumentException("Illumination must be in range 300 - 1300");

            _transport.SendWriteCommand(Sid, new GatewayLightCommand(rgb, illumination).ToString());
        }

        public void DisableLight()
        {
            _transport.SendWriteCommand(Sid, new GatewayLightCommand(0, 0).ToString());
        }
    }
}