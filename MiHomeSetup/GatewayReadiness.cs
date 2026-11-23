namespace MiHomeSetup;

internal sealed record GatewayReadiness(bool Mqtt, bool Discovery, Exception? DiscoveryError = null)
{
    internal bool Ready => Mqtt && Discovery;
}
