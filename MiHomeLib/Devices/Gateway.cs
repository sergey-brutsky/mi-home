using Newtonsoft.Json.Linq;

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

            Rgb = jObject["rgb"].ToString();
            Illumination = jObject["illumination"].ToString();
            ProtoVersion = jObject["proto_version"].ToString();
        }

        public void EnableLight()
        {
            _transport.SendWriteCommand(Sid, "{\"rgb\":1694498786,\"illumination\":1000}");
        }

        public void DisableLight()
        {
            _transport.SendWriteCommand(Sid, "{\"rgb\":0,\"illumination\":0}");
        }
    }
}