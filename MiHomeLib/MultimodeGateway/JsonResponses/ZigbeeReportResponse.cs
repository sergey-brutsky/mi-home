using System.Collections.Generic;

namespace MiHomeLib.MultimodeGateway.JsonResponses;

public class ZigbeeReportResponse : MiioResponse
{
    public string Cmd { get; } = "report";
    public string Did { get; set; }
    public double Time { get; set; }
    public int Rssi { get; set; }
    public int Zseq { get; set; }
    public List<ZigbeeReportResource> @Params { get; set; }
    public List<ZigbeeMiSpecItem> @MiSpec { get; set; }

    public string DevSrc { get; set; }

    public class ZigbeeReportResource: MiioResponse
    {
        public string ResName { get; set; }
        public int Value { get; set; }
    }

    public class ZigbeeMiSpecItem
    {
        public int Siid { get; set; }
        public int Piid { get; set; }
        public object Value { get; set; }
    }
}
