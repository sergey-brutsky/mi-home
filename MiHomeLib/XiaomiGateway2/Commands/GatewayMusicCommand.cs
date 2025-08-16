namespace MiHomeLib.XiaomiGateway2.Commands;

public class GatewayMusicCommand(int midNo) : Command
{
    private readonly int _midNo = midNo;

    public override string ToString() => $"{{\"mid\":{_midNo}}}";
}