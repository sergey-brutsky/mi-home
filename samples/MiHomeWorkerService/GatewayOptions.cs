using System.Collections.Generic;

namespace MiHomeWorkerService;

public enum GatewayKind
{
    XiaomiGateway2,
    MultimodeGateway,
    MultimodeGateway2China,
    MultimodeGateway2Global,
    AqaraHubE1China,
}

public sealed class GatewayOptions
{
    public string Name { get; set; }
    public GatewayKind Kind { get; set; }
    public string Ip { get; set; }
    public string Token { get; set; }
    public string Did { get; set; } = "";
    public string GatewaySid { get; set; }
    public int MqttPort { get; set; } = 1883;
}

public sealed class MiHomeOptions
{
    public List<GatewayOptions> Gateways { get; set; } = new();
}
