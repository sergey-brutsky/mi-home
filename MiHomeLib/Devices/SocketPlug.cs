using System.Globalization;
using MiHomeLib.Commands;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class SocketPlug : MiHomeDevice<SocketPlug>
    {
        public static string IdString => "plug";

        public override string Type => IdString;

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
            var hasChanges = false;

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out var voltage))
                Voltage = voltage / 1000;

            if (jObject["inuse"] != null && int.TryParse(jObject["inuse"].ToString(), out var inuse))
                hasChanges |= ChangeAndDetectChanges(() => Inuse, inuse);

            if (jObject["power_consumed"] != null && int.TryParse(jObject["power_consumed"].ToString(), out var powerConsumed))
                hasChanges |= ChangeAndDetectChanges(() => PowerConsumed, powerConsumed);

            if (jObject["load_power"] != null && float.TryParse(jObject["load_power"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out var loadPower))
                hasChanges |= ChangeAndDetectChanges(() => LoadPower, loadPower);

            if (jObject["status"] != null)
                hasChanges |= ChangeAndDetectChanges(() => Status, jObject["status"].ToString());

            if (hasChanges)
                _changes.OnNext(this);
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