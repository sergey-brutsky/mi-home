using System;
using System.Linq;
using System.Text.Json.Nodes;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class MiotGenericDevice(IMiioTransport miioTransport, int initialIdExternal) : MiioDevice(miioTransport, initialIdExternal)
{
    protected string GetMiotProperty(int siid, int piid, string did = null) => GetMiotProperties([(siid, piid, did ?? $"{siid}-{piid}")])[0];
    protected string[] GetMiotProperties((int siid, int piid)[] arr)
    {
        return GetMiotProperties(arr.Select(x => (x.siid, x.piid, $"{x.siid}-{x.piid}")).ToArray());
    }
    protected string[] GetMiotProperties((int siid, int piid, string did)[] arr)
    {
        var msg = BuildParamsArray("get_properties", [.. arr.Select(x => new {
            x.did,
            x.siid,
            x.piid
        })]);

        var response = RepeatMessageIfTimeout(_miioTransport.SendMessage, msg);

        var values = JsonNode.Parse(response)["result"]
            .AsArray()
            .ToDictionary(x => x["did"].ToString(), x => x["value"].ToString());

        return [.. arr.Select(x => values[x.did])];
    }

    protected void SetMiotProperty(int siid, int piid, object value = null)
    {
        SetMiotProperty(siid, piid, $"set-{siid}-{piid}", value);
    }
    protected void SetMiotProperty(int siid, int piid, string did, object value = null)
    {
        var msg = BuildParamsArray("set_properties", [new { did, siid, piid, value }]);

        var response = RepeatMessageIfTimeout(_miioTransport.SendMessage, msg);

        var values = JsonNode.Parse(response)["result"].AsArray()[0];

        if (int.Parse(values["code"].ToString()) != 0)
            throw new Exception($"Cannot get property {siid}/{piid} from devices");

        return;
    }
    protected void CallMiotAction(int siid, int aiid, params object[] args)
    {
        var msg = BuildParamsObject("action", new { did = $"call-{siid}-{aiid}", siid, aiid, @in = args });

        var response = RepeatMessageIfTimeout(_miioTransport.SendMessage, msg);

        if (int.Parse(JsonNode.Parse(response)["result"]["code"].ToString()) != 0)
            throw new Exception($"Cannot call action siid: {siid}, aiid: {aiid} for this device");

        return;
    }
}
