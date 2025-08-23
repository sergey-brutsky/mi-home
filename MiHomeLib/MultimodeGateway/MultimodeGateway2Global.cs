namespace MiHomeLib.MultimodeGateway;

// Since there is another modification of Multimode Gateway 2 for CN market
// and I don't know if there are software differences, implementing
// region specific MultimodeGateway2Global version

/// <summary>
/// Xiaomi Multimode Gateway 2 (Global) ZNDMWG04LM
/// </summary>
public class MultimodeGateway2Global(string ip, string token, int port = 1883) : MultimodeGateway2(ip, token, port)
{
    public new const string MARKET_MODEL = "ZNDMWG04LM";
    public new const string MODEL = "lumi.gateway.mgl001";
}
