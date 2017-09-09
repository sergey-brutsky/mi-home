namespace MiHomeLib.Commands
{
    internal class GatewayMusicCommand : Command
    {
        private readonly int _midNo;

        public GatewayMusicCommand(int midNo)
        {
            _midNo = midNo;
        }

        public override string ToString()
        {
            return $"{{\"mid\":{_midNo}}}";
        }
    }
}