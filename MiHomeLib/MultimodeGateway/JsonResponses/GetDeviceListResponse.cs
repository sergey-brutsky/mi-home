using System.Collections.Generic;

namespace MiHomeLib.MultimodeGateway.JsonResponses;

public class GetDeviceListResponse : MiioResponse
{
    public int Code { get; set; }
    public List<GetDeviceListItem> Result { get; set; }

    public int ExeTime { get; set; }

    public class GetDeviceListItem : MiioResponse
    {
        public string Did { get; set; }
        public string Model { get; set; }
        public int Num { get; set; }
        public int Total { get; set; }
    }
}
