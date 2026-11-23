using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeSetup;

internal static class Gateway2Checker
{
    internal const string Model = XiaomiGateway2.MODEL;

    internal static async Task<bool> RunAsync(SetupOptions options, GatewayInfo knownInfo, CancellationToken token, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        if (options.Token is null && options.TokenFile is null)
            throw new ArgumentException("Gateway 2 checks require --token or --token-file with the gateway's miIO token.");
        var secret = await options.ReadTokenAsync(token);
        using var transport = new MiioTransport(options.Target, secret) { UseGateway2RequestFormat = true };
        using var stopTransport = token.Register(transport.Dispose);
        try
        {
            // Verify on this same connection, matching miiocli's info(id=1), command(id=2) exchange.
            var info = await GatewayModelDetector.ReadMiioInfoAsync(transport, token);
            if (info.Model != Model)
                throw new InvalidOperationException("The gateway reported a different model. Run check without --model to detect it.");
            if (options.Command == "disable-lan")
            {
                await Gateway2Lan.DisableAsync(transport, token, output);
                return true;
            }
            var sid = SidFromMac(info.Mac);
            var requestedKey = await options.ReadKeyAsync(token);
            if (options.Command == "setup")
                await Gateway2Lan.EnsureEnabledAsync(transport, requestedKey, token, output);
            output.WriteLine("PASS miIO connection and model verification");
            using var gateway = new XiaomiGateway2(transport, new UdpTransport(loggerFactory: NullLoggerFactory.Instance), sid,
                loggerFactory: NullLoggerFactory.Instance);
            return await CheckAsync(gateway, token, output, discoveryAttempts: options.Command == "setup" ? 3 : 1);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
    }

    internal static async Task<bool> CheckAsync(XiaomiGateway2 gateway, CancellationToken token, TextWriter? output = null,
        int discoveryAttempts = 1, TimeSpan? retryDelay = null)
    {
        output ??= TextWriter.Null;
        token.ThrowIfCancellationRequested();
        using var stop = token.Register(gateway.Dispose);
        var discovered = false;
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await gateway.DiscoverDevicesAsync().WaitAsync(token);
                    break;
                }
                catch (TimeoutException) when (attempt < discoveryAttempts)
                {
                    output.WriteLine("Waiting for Gateway 2 UDP LAN access to become available...");
                    await Task.Delay(retryDelay ?? TimeSpan.FromSeconds(1), token);
                }
            }
            output.WriteLine("PASS Gateway 2 UDP discovery (" + gateway.GetDevices().Count + " supported devices found)");
            discovered = true;
        }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception)
        {
            output.WriteLine("FAIL Gateway 2 UDP discovery. Check LAN API/developer mode, multicast 224.0.0.50 and UDP port 9898 in both directions.");
        }
        try
        {
            var developerKey = await gateway.GetDeveloperKeyAsync().WaitAsync(token);
            if (developerKey.Length != 16) throw new InvalidDataException("Invalid developer key length.");
            output.WriteLine("PASS Developer key is available. Sub-device writes require separate verification.");
        }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception)
        {
            output.WriteLine("WARN Developer key is unavailable. Configure developer mode for sub-device writes.");
            WriteInstructions(output);
        }
        output.WriteLine(discovered ? "READY (miIO and Gateway 2 UDP discovery)" : "NOT READY");
        if (!discovered) WriteInstructions(output);
        return discovered;
    }

    internal static string SidFromMac(string? mac)
    {
        if (mac is null || mac.Length != 17 || mac.Split(':').Any(part => part.Length != 2 || !part.All(char.IsAsciiHexDigit)))
            throw new InvalidDataException("miIO.info did not return a valid MAC address. Gateway 2 discovery needs it to select this gateway among multicast responses.");
        return mac.Replace(":", "").ToLowerInvariant();
    }

    private static void WriteInstructions(TextWriter output)
    {
        output.WriteLine("Enable the Gateway 2 LAN API with make gw setup HOST token=\"<MIIO_TOKEN>\", replacing HOST with this gateway IP. Then rerun check.");
        output.WriteLine("Gateway 2 uses its built-in miIO and UDP protocols. Setup reuses a valid current developer key, creates one when empty, or applies the supplied --key/--key-file.");
        output.WriteLine("https://github.com/sergey-brutsky/mi-home/wiki");
    }
}
