namespace MiHomeSetup;

internal interface IGatewayShell
{
    Task<string> RunAsync(string command, CancellationToken token);
}
