using System.Globalization;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SocketPlug : MiHomeDevice
    {
        private readonly UdpTransport _transport;
        public SocketPlug(string sid, UdpTransport transport) : base(sid)
        {
            _transport = transport;
        }

        public float? Voltage { get; private set; }
        public string Status { get; private set; }
        public int? Inuse { get; private set; }
        public int? PowerConsumed { get; private set; }
        public float? LoadPower { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            float.TryParse(jObject["voltage"].ToString(), out float voltage);
            int.TryParse(jObject["inuse"].ToString(), out int inuse);
            int.TryParse(jObject["power_consumed"].ToString(), out int powerConsumed);
            float.TryParse(jObject["load_power"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out float loadPower);

            Status = jObject["status"].ToString();

            Voltage = voltage;
            Inuse = inuse;
            PowerConsumed = powerConsumed;
            LoadPower = loadPower;
        }
    }
}