// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mgl001
// Your contributions are appreciated
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway;

/// <summary>
/// Xiaomi Multimode Gateway 2 (Global) ZNDMWG04LM
/// </summary>
public class MultimodeGateway2Global : MultimodeGateway2
{
    public new const string MARKET_MODEL = "ZNDMWG04LM";
    public new const string MODEL = "lumi.gateway.mgl001";

    public MultimodeGateway2Global(string ip, string token, string did, int port = 1883) : base(ip, token, did, port)
    {
        _devicesDiscoverer = new MultimodeGateway2GlobalDevicesDiscoverer(ip, 23);
        _logger = _loggerFactory.CreateLogger<MultimodeGateway2Global>();
    }

    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
