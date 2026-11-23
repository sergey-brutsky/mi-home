// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mgl001
// Your contributions are appreciated
using Microsoft.Extensions.Logging;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.MqttGateway;

/// <summary>
/// Xiaomi Multimode Gateway 2 (Global) ZNDMWG04LM
/// </summary>
public class MultimodeGateway2Global : MultimodeGateway2Base
{
    public const string MARKET_MODEL = "ZNDMWG04LM";
    public const string MODEL = "lumi.gateway.mgl001";

    public MultimodeGateway2Global(string ip, string token, string did, int port = 1883): base(ip, token, did, port, "/ # ")
    { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by MiHomeLogging.SetLoggerFactory</param>
    /// <param name="did">Gateway device identifier.</param>
    /// <param name="ip">Gateway IP address.</param>
    /// <param name="port">Gateway service port.</param>
    /// <param name="token">Gateway miIO token.</param>
    public MultimodeGateway2Global(string ip, string token, string did, int port, ILoggerFactory loggerFactory): base(ip, token, did, port, "/ # ", loggerFactory) { }
    
    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
