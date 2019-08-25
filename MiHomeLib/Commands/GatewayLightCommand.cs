namespace MiHomeLib.Commands
{
    internal class GatewayLightCommand : Command
    {
        private readonly long _rgb;

        public GatewayLightCommand(long rgb)
        {
            _rgb = rgb;
        }

        public override string ToString()
        {
            return $"{{\"rgb\":{_rgb}}}";
        }
    }
}