namespace MiHomeLib.Commands;

public class GatewayMusicCommand(int midNo) : Command
{
    private readonly int _midNo = midNo;

    public override string ToString() => $"{{\"mid\":{_midNo}}}";
}