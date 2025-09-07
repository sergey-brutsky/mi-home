// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mcn001
// Your contributions are appreciated
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway;

/// <summary>
/// Xiaomi Multimode Gateway 2 (China) DMWG03LM
/// </summary>
public class MultimodeGateway2China : MultimodeGateway2
{
    public const string MARKET_MODEL = "DMWG03LM";
    public const string MODEL = "lumi.gateway.mcn001";

    public MultimodeGateway2China(string ip, string token, string did, int port = 1883) : base(ip, token, did, port)
    {
        _logger = _loggerFactory.CreateLogger<MultimodeGateway2China>();
    }
    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
