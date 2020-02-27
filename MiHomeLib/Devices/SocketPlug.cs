using System.Globalization;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SocketPlug : MiHomeDevice
    {
        private readonly IMessageTransport _transport;
        public SocketPlug(string sid, IMessageTransport transport) : base(sid, "plug")
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

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out float voltage))
            {
                Voltage = voltage / 1000;
            }

            if (jObject["inuse"] != null && int.TryParse(jObject["inuse"].ToString(), out int inuse))
            {
                Inuse = inuse;
            }

            if (jObject["power_consumed"] != null && int.TryParse(jObject["power_consumed"].ToString(), out int powerConsumed))
            {
                PowerConsumed = powerConsumed;
            }

            if (jObject["load_power"] != null && float.TryParse(jObject["load_power"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out float loadPower))
            {
                LoadPower = loadPower;
            }

            if (jObject["status"] != null)
            {
                Status = jObject["status"].ToString();
            }
        }

        public override string ToString()
        {
            return $"Status: {Status}, Inuse: {Inuse}, Load Power: {LoadPower}V, Power Consumed: {PowerConsumed}W, Voltage: {Voltage}V";
        }

        public void TurnOff()
        {
            _transport.SendWriteCommand(Sid, Type, new SocketPlugCommand("off"));
        }

        public void TurnOn()
        {
            _transport.SendWriteCommand(Sid, Type, new SocketPlugCommand());
        }
    }
}