using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiHomeLib.MiioDevices.Internal;

namespace MiHomeSetup;

internal static class GatewayModelDetector
{
    internal static async Task<GatewayInfo> DetectAsync(SetupOptions options, CancellationToken token, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        if (options.Token is not null || options.TokenFile is not null)
        {
            try
            {
                var info = await ReadMiioInfoAsync(options, token);
                output.WriteLine("Detected model via miIO: " + info.Model);
                return info;
            }
            catch (Exception exception) when (!token.IsCancellationRequested &&
                exception is IOException or SocketException or TimeoutException or JsonException)
            {
                output.WriteLine("miIO model detection unavailable. Trying the existing Telnet connection.");
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
        }
        try
        {
            using var shell = new GatewayShell(TimeSpan.FromSeconds(5));
            var profile = await shell.ConnectAsync(options.Target, null, token);
            var modelOutput = await shell.RunAsync(profile.ModelCommand, token);
            var model = ParseShellModel(modelOutput, profile.Architecture);
            GatewayProfile.ValidateModel(model);
            output.WriteLine("Detected model via Telnet: " + model);
            return new GatewayInfo(model, null);
        }
        catch (Exception exception) when (!token.IsCancellationRequested &&
            exception is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException)
        {
            throw new IOException("Cannot detect the gateway model. Specify --model explicitly, or supply --token/--token-file for miIO.info. Detection without a token requires existing passwordless Telnet on a recognized gateway.", exception);
        }
    }

    internal static async Task<GatewayInfo> ReadMiioInfoAsync(SetupOptions options, CancellationToken token)
    {
        var secret = await options.ReadTokenAsync(token);
        using var transport = new MiioTransport(options.Target, secret);
        using var registration = token.Register(transport.Dispose);
        try { return await ReadMiioInfoAsync(transport, token); }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
    }

    internal static async Task<GatewayInfo> ReadMiioInfoAsync(IMiioRequests transport, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var response = await transport.RequestAsync("miIO.info", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat).WaitAsync(token);
        using var document = JsonDocument.Parse(response);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("model", out var modelValue) || modelValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(modelValue.GetString()))
            throw new InvalidDataException("miIO.info did not return a gateway model.");
        var model = modelValue.GetString()!;
        GatewayProfile.ValidateModel(model);
        var firmware = result.TryGetProperty("fw_ver", out var firmwareValue) && firmwareValue.ValueKind == JsonValueKind.String
            ? firmwareValue.GetString() : null;
        var mac = result.TryGetProperty("mac", out var macValue) && macValue.ValueKind == JsonValueKind.String
            ? macValue.GetString() : null;
        return new GatewayInfo(model, firmware, mac);
    }

    internal static string ParseShellModel(string output, string architecture)
    {
        var models = architecture == "mips"
            ? output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Distinct().ToArray()
            : Regex.Matches(output, @"(?m)^\[persist[^\]\r\n]*\.model\]:\s*\[([^\]\r\n]+)\]\r?$", RegexOptions.CultureInvariant)
                .Select(match => match.Groups[1].Value).Distinct().ToArray();
        if (models.Length != 1 || string.IsNullOrWhiteSpace(models[0]))
            throw new InvalidDataException("The gateway did not return one unambiguous model.");
        return models[0];
    }
}
