namespace MiHomeLib.XiaomiGateway2.Commands;

public class GatewayLightCommand(long rgb) : Command
{
    private readonly long _rgb = rgb;

    public override string ToString()
    {
        return $"{{\"rgb\":{_rgb}}}";
    }
}