// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mgl001
// Your contributions are appreciated
using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;
using MiHomeLib.MiioDevices;
using MiHomeLib.Transport;

namespace MiHomeLib.MqttGateway;

/// <summary>
/// Xiaomi Multimode Gateway 2 (Global) ZNDMWG04LM
/// </summary>
public class MultimodeGateway2Global : MultimodeGateway2Base
{
    public const string MARKET_MODEL = "ZNDMWG04LM";
    public const string MODEL = "lumi.gateway.mgl001";

    public MultimodeGateway2Global(string ip, string token, string did, int port = 1883): base(ip, token, did, port, "/ # ")
    {
        _logger = _loggerFactory.CreateLogger<MultimodeGateway2Global>();
    }
    
    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
