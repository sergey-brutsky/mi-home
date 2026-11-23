using System.Net;

namespace MiHomeSetup;

internal sealed record SetupOptions(string Command, string Target, string? Model, string? TokenFile, string? KeyFile, string? Token, string? Key)
{
    internal static SetupOptions Parse(string[] args)
    {
        if (args.Length < 3 || args.Length % 2 == 0 || args[0] is not ("check" or "setup" or "disable-lan" or "download-agent"))
            throw new ArgumentException("Use --help for usage.");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var targetKey = args[0] == "download-agent" ? "--output" : "--host";
        for (var i = 1; i < args.Length; i += 2)
        {
            var name = args[i];
            if (name != targetKey && name != "--model" &&
                !(args[0] is "setup" or "check" or "disable-lan" && (name is "--token" or "--token-file")) &&
                !(args[0] is "setup" or "check" && (name is "--key" or "--key-file")))
                throw new ArgumentException("Unknown option. Use --help for usage.");
            if (string.IsNullOrWhiteSpace(args[i + 1]) || !options.TryAdd(name, args[i + 1]))
                throw new ArgumentException("Options must have nonempty values and appear only once.");
        }
        if (!options.TryGetValue(targetKey, out var target))
            throw new ArgumentException("Provide " + targetKey + ".");
        options.TryGetValue("--model", out var model);
        if (model is null && args[0] == "download-agent")
            throw new ArgumentException("--model is required for " + args[0] + ".");
        if (args[0] != "download-agent" && !IPAddress.TryParse(target, out _))
            throw new ArgumentException("--host must be an IP address.");
        if (model is not null) GatewayProfile.ValidateModel(model);
        if (args[0] == "disable-lan" && model is not null && model != Gateway2Checker.Model)
            throw new ArgumentException("disable-lan supports only Gateway 2 (lumi.gateway.v3).");
        if (model == Gateway2Checker.Model && args[0] == "download-agent")
            throw new ArgumentException("Gateway 2 uses its native UDP protocol. download-agent is available for MQTT gateways.");
        options.TryGetValue("--token-file", out var tokenFile);
        options.TryGetValue("--token", out var token);
        options.TryGetValue("--key-file", out var keyFile);
        options.TryGetValue("--key", out var key);
        if (token is not null && tokenFile is not null)
            throw new ArgumentException("Specify either --token or --token-file.");
        if (key is not null && keyFile is not null)
            throw new ArgumentException("Specify either --key or --key-file.");
        if ((key is not null || keyFile is not null) && tokenFile is null && token is null)
            throw new ArgumentException("--key and --key-file require --token or --token-file.");
        return new(args[0], target, model, tokenFile, keyFile, token, key);
    }

    internal async Task<string> ReadTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var secret = Token;
        if (secret is null)
        {
            if (TokenFile is null)
                throw new InvalidOperationException("Telnet is closed. Provide --token or --token-file with the gateway's 32-character miIO token to enable it, or enable Telnet manually.");
            secret = await File.ReadAllTextAsync(TokenFile, cancellationToken);
        }
        secret = secret.Trim();
        if (secret.Length != 32 || !secret.All(char.IsAsciiHexDigit))
            throw new ArgumentException("The miIO token must contain exactly 32 hexadecimal characters.");
        return secret;
    }

    internal async Task<string?> ReadKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key;
        if (key is null && KeyFile is not null)
            key = await File.ReadAllTextAsync(KeyFile, cancellationToken);
        if (key is null) return null;
        key = key.Trim();
        ValidateKey(key);
        return key;
    }

    internal static void ValidateKey(string key)
    {
        if (key.Length != 16 || key.Any(character => character < '!' || character > '~'))
            throw new ArgumentException("The gateway key must contain exactly 16 printable ASCII characters. Use the gateway key, not the miIO token.");
    }
}
