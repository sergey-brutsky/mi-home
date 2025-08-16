using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiMultifunctionalGateway2 : XiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "DGNWG02LM";
    public const string MODEL = "gateway";

    public XiaomiMultifunctionalGateway2(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Actions.Add("rgb", x => 
        {
            Rgb = x.GetInt32();
        });

        Actions.Add("illumination", x => 
        {
            Illumination = x.GetInt32();
        });

        Actions.Add("proto_version", x => 
        {
            ProtoVersion = x.GetString();
        });

        Actions.Add("ip", x => 
        {
            Ip = x.GetString();
        });
    }
    public string Ip { get; private set; }
    public int Rgb { get; private set; }
    public int Illumination { get; private set; }
    public string ProtoVersion { get; private set; }        
    public override string ToString() => base.ToString() + $", Rgb: {Rgb}, Illumination: {Illumination}, ProtoVersion: {ProtoVersion}";
}