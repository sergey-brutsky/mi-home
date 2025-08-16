using System.Collections.Generic;

namespace MiHomeLib.XiaomiGateway3.JsonResponses;

public class BleAsyncEventResponse : BleResponse
{
    public string Method { get; } = "_async.ble_event";

    public BleAsyncEventParams @Params { get; set; }

    public class BleAsyncEventParams: BleResponse
    {
        public BleAsyncEventDevice Dev { get; set; }
        public List<BleAsyncEventEvt> Evt { get; set; }
        public int FromCnt { get; set; }

        public long Gwts { get; set; }

        public class BleAsyncEventDevice
        {
            public string Did { get; set; }
            public string Mac { get; set; }
            public int Pdid { get; set; }
        }

        public class BleAsyncEventEvt
        {
            public int Eid { get; set; }
            public string Edata { get; set; }
        }
    }
}
