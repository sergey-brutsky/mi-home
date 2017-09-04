using System.Globalization;
using System.Linq;
using MiHomeLib.Commands;
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

            if (float.TryParse(jObject["voltage"].ToString(), out float voltage))
            {
                Voltage = voltage / 1000;
            }

            if (int.TryParse(jObject["inuse"].ToString(), out int inuse))
            {
                Inuse = inuse;
            }

            if (jObject.Children().Contains("power_consumed") && int.TryParse(jObject["power_consumed"].ToString(), out int powerConsumed))
            {
                PowerConsumed = powerConsumed;
            }

            if (jObject.Children().Contains("load_power") && float.TryParse(jObject["load_power"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out float loadPower))
            {
                LoadPower = loadPower;
            }

            Status = jObject["status"].ToString();
        }

        public void TurnOff()
        {
            _transport.SendWriteCommand(Sid, new SocketPlugCommand("off").ToString());
        }

        public void TurnOn()
        {
            _transport.SendWriteCommand(Sid, new SocketPlugCommand("on").ToString());
        }
    }
}