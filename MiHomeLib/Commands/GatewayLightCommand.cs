namespace MiHomeLib.Commands
{
    internal class GatewayLightCommand
    {
        private readonly long _rgb;
        private readonly int _illumination;

        public GatewayLightCommand(long rgb, int illumination)
        {
            _rgb = rgb;
            _illumination = illumination;
        }

        public override string ToString()
        {
            return $"{{\"rgb\":{_rgb},\"illumination\":{_illumination}}}";
        }
    }
}