using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class MiotGenericDevice: MiioDevice
{   
    protected readonly string _did;
    public uint UptimeSeconds { get; private set; }
    public string MiioVersion { get; private set; }
    public string Mac { get; private set; }
    public string FirmwareVersion { get; private set; }
    public string Hardware { get; private set; }
    public WifiSettings Wifi { get; private set; }
    public NetifSettings Network { get; private set; }

    public MiotGenericDevice(string did, IMiioTransport miioTransport, int initialIdExternal) : base(miioTransport, initialIdExternal)
    {
        _did = did;

        var response = _miioTransport.SendMessage(BuildParamsArray("miIO.info", string.Empty));

        var values = JsonNode
            .Parse(response)["result"]
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        UptimeSeconds = uint.Parse(values["life"]);
        MiioVersion = values["miio_ver"].ToString();
        Mac = values["mac"].ToString();
        FirmwareVersion = values["fw_ver"].ToString();
        Hardware = values["hw_ver"].ToString();

        var apValues = JsonNode
            .Parse(values["ap"].ToString())
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        Wifi = new WifiSettings()
        {
            Ssid = apValues["ssid"].ToString(),
            Bssid = apValues["bssid"].ToString(),
            Rssi = int.Parse(apValues["rssi"]),
            Freq = int.Parse(apValues["freq"]),
        };

        var netifValues = JsonNode
            .Parse(values["netif"].ToString())
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        Network = new NetifSettings()
        {
            Ip = netifValues["localIp"].ToString(),
            Mask = netifValues["mask"].ToString(),
            Gateway = netifValues["gw"].ToString(),
        };
    }

    public class WifiSettings
    {
        public string Ssid { get; internal set; }
        public string Bssid { get; internal set; }
        public int Rssi { get; internal set; }
        public int Freq { get; internal set; }
    }
    public class NetifSettings
    {
        public string Ip { get; internal set; }
        public string Mask { get; internal set; }
        public string Gateway { get; internal set; }
    }
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
