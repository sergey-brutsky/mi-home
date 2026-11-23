using System.Security.Cryptography;
using System.Text.Json;
using MiHomeLib.MiioDevices.Internal;

namespace MiHomeSetup;

internal static class Gateway2Lan
{
    internal static async Task EnsureEnabledAsync(IMiioRequests transport, string? requestedKey,
        CancellationToken token, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        if (requestedKey is not null) SetupOptions.ValidateKey(requestedKey);
        var current = await ReadKeyAsync(transport, token);
        if (requestedKey is null && current.Length == 16)
        {
            output.WriteLine("Gateway 2 LAN access already has a developer key. Reusing the current key.");
            return;
        }
        if (requestedKey is null && current.Length != 0)
            throw new InvalidDataException("The current Gateway 2 developer key has an incompatible length. Supply --key with a valid 16-character key to replace it.");
        var desired = requestedKey ?? RandomNumberGenerator.GetString("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", 16);
        if (current != desired) await SetAndVerifyAsync(transport, desired, token);
        output.WriteLine("Gateway 2 developer key is configured. Checking UDP LAN access next.");
    }

    internal static async Task DisableAsync(IMiioRequests transport, CancellationToken token, TextWriter? output = null,
        TimeSpan? verificationDelay = null)
    {
        await SetAndVerifyAsync(transport, "", token, verificationDelay);
        (output ?? TextWriter.Null).WriteLine("Gateway 2 developer key is empty. LAN access has been disabled; run setup to enable it again.");
    }

    private static async Task SetAndVerifyAsync(IMiioRequests transport, string key, CancellationToken token,
        TimeSpan? verificationDelay = null)
    {
        token.ThrowIfCancellationRequested();
        var acknowledged = false;
        try
        {
            var response = await transport.RequestAsync("set_lumi_dpf_aes_key", new[] { key }, MiioRetryPolicy.DoNotRepeat).WaitAsync(token);
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array ||
                result.GetArrayLength() != 1 || result[0].ValueKind != JsonValueKind.String || result[0].GetString() != "ok")
                throw new InvalidOperationException("The gateway rejected the developer-key update.");
            acknowledged = true;
        }
        catch (TimeoutException)
        {
            // A missing acknowledgement does not prove the update failed. Verify without replaying it.
        }
        // Some firmware may expose the previous value immediately after acknowledging a write.
        // Repeat only the read, never the state-changing request.
        string actual = "";
        const int attempts = 5;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0) await Task.Delay(verificationDelay ?? TimeSpan.FromMilliseconds(250), token);
            actual = await ReadKeyAsync(transport, token);
            if (actual == key) return;
        }
        var writeStatus = acknowledged ? "acknowledged the write with ok" : "did not acknowledge the write before timeout";
        var expectedState = key.Length == 0 ? "an empty developer key" : "the requested developer key";
        throw new InvalidOperationException($"The gateway {writeStatus}, but {attempts} readback checks did not return {expectedState} " +
            $"(returned string length: {actual.Length}; key value hidden). " +
            (key.Length == 0
                ? "LAN access disable could not be confirmed. Check the LAN access switch in Mi Home and compare with miiocli; an ok reply alone does not confirm a state change."
                : "The developer-key update could not be confirmed. Run check before retrying setup."));
    }

    private static async Task<string> ReadKeyAsync(IMiioRequests transport, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var response = await transport.RequestAsync("get_lumi_dpf_aes_key", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat).WaitAsync(token);
        using var document = JsonDocument.Parse(response);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array ||
            result.GetArrayLength() != 1 || result[0].ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Cannot read the Gateway 2 developer-key state. Verify miIO access before changing LAN settings.");
        return result[0].GetString()!;
    }
}
