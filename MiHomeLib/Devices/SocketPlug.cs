using System.Globalization;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SocketPlug : MiHomeDevice
    {
        public const string TypeKey = "plug";

        private readonly IMessageTransport _transport;

        public SocketPlug(string sid, IMessageTransport transport) : base(sid, TypeKey)
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

            Voltage = jObject.ParseVoltage();

            if (jObject.ParseString("status", out string status))
            {
                Status = status;
            }

            if (jObject.ParseInt("inuse", out int inuse))
            {
                Inuse = inuse;
            }

            if (jObject.ParseInt("power_consumed", out int powerConsumed))
            {
                PowerConsumed = powerConsumed;
            }

            if (jObject.ParseFloat("load_power", out float loadPower))
            {
                LoadPower = loadPower;
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
            _transport.SendWriteCommand(Sid, Type, new SocketPlugCommand("on"));
        }
    }
}