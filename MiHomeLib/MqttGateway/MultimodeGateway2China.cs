// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/lumi.gateway.mcn001
// Your contributions are appreciated
using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.MqttGateway;

/// <summary>
/// Xiaomi Multimode Gateway 2 (China) DMWG03LM
/// </summary>
public class MultimodeGateway2China : MultimodeGateway2Base
{
    public const string MARKET_MODEL = "DMWG03LM";
    public const string MODEL = "lumi.gateway.mcn001";

    public MultimodeGateway2China(string ip, string token, string did, int port = 1883): base(ip, token, did, port, "~ # ")
    { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by MiHomeLogging.SetLoggerFactory</param>
    public MultimodeGateway2China(string ip, string token, string did, int port, ILoggerFactory loggerFactory): base(ip, token, did, port, "~ # ", loggerFactory) { }
    
    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
