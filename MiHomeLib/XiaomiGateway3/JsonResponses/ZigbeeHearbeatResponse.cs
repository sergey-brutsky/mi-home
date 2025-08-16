using System.Collections.Generic;

namespace MiHomeLib.XiaomiGateway3.JsonResponses;

public class ZigbeeHearbeatResponse : MiioResponse
{
    public string Cmd { get; } = "heartbeat";
    public long Id { get; set; }
    public double Time { get; set; }
    public int Rssi { get; set; }
    public List<ZigbeeHearbeatItem> @Params { get; set; }

    public class ZigbeeHearbeatItem
    {
        public string Did { get; set; }
        public double Time { get; set; }
        public int Zseq { get; set; }
        public List<ZigbeeHearbeatItemResource> ResList { get; set; }

        public class ZigbeeHearbeatItemResource
        {
            public string ResName { get; set; }
            public int Value { get; set; }
        }
    }
}
