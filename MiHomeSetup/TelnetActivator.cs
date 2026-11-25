using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiHomeLib.MiioDevices.Internal;

namespace MiHomeSetup;

internal static class TelnetActivator
{
    // Same gateway preparation command as XiaomiGateway3 core/core_utils.py.
    // This enables passwordless administrative access on the gateway's LAN interface.
    private const string TelnetCommand = "passwd -d $USER; riu_w 101e 53 3012 || echo enable > /sys/class/tty/tty/enable; telnetd";

    internal static async Task EnsureAsync(SetupOptions options, CancellationToken token, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        if (await IsOpenAsync(options.Target, token))
        {
            output.WriteLine("Telnet port is already accessible. Checking administrative login next.");
            return;
        }
        var secret = await options.ReadTokenAsync(token);
        var key = await options.ReadKeyAsync(token);
        using var transport = new MiioTransport(options.Target, secret);
        using var registration = token.Register(transport.Dispose);
        try
        {
            await EnableAsync(options.Model!, transport, () => transport.DeviceId, key,
                cancellation => IsOpenAsync(options.Target, cancellation), token, output: output);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
    }

    internal static async Task EnableAsync(string expectedModel, IMiioRequests miio, Func<uint?> deviceId,
        string? key, Func<CancellationToken, Task<bool>> isOpen, CancellationToken token, TimeSpan? pollDelay = null, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        token.ThrowIfCancellationRequested();
        var response = await miio.RequestAsync("miIO.info", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat, token).WaitAsync(token);
        using var info = JsonDocument.Parse(response);
        if (info.RootElement.ValueKind != JsonValueKind.Object || !info.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("miIO.info did not return gateway information. Verify the miIO token and connectivity.");
        var model = ReadString(result, "model");
        if (model != expectedModel)
            throw new InvalidOperationException("The miIO gateway model differs from --model. Telnet activation stopped.");
        var firmwareText = ReadString(result, "fw_ver");
        var method = GatewayFirmware.Parse(firmwareText).TelnetMethod(model);
        output.WriteLine("miIO model: " + model + "; firmware: " + firmwareText + "; Telnet method: " + method);
        object? parameters = null;
        if (method == "set_ip_info") parameters = new { ssid = "\"\"", pswd = "1; " + TelnetCommand };
        if (method == "system_command")
        {
            if (key is null)
                throw new InvalidOperationException("This firmware requires the separate 16-character gateway key. Provide --key or --key-file using your saved XiaomiGateway3 gateway key, or enable Telnet manually. The miIO token is a different credential.");
            SetupOptions.ValidateKey(key);
            var mac = ReadString(result, "mac");
            if (mac.Length != 17 || !mac.Split(':').All(part => part.Length == 2 && part.All(char.IsAsciiHexDigit)))
                throw new InvalidDataException("miIO.info returned an invalid gateway MAC address.");
            var did = deviceId() ?? throw new InvalidDataException("miIO handshake did not provide a gateway device ID.");
            parameters = new { password = CreatePassword(did, mac, key), command = TelnetCommand };
        }
        token.ThrowIfCancellationRequested();
        output.WriteLine("Enabling passwordless administrative Telnet on the selected gateway...");
        // An activation request is sent once. If its acknowledgement is lost, check port 23 before declaring failure.
        var acknowledged = false;
        try
        {
            var reply = await miio.RequestAsync(method, parameters!, MiioRetryPolicy.DoNotRepeat, token).WaitAsync(token);
            using var document = JsonDocument.Parse(reply);
            acknowledged = document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("result", out var answer) &&
                answer.ValueKind == JsonValueKind.Array && answer.GetArrayLength() == 1 &&
                answer[0].ValueKind == JsonValueKind.String && answer[0].GetString() == "ok";
        }
        catch (TimeoutException) { }
        catch (JsonException) { }
        for (var attempt = 0; attempt < 5; attempt++)
        {
            token.ThrowIfCancellationRequested();
            if (await isOpen(token))
            {
                output.WriteLine("Telnet port is accessible. Checking administrative login next.");
                return;
            }
            if (attempt < 4) await Task.Delay(pollDelay ?? TimeSpan.FromSeconds(1), token);
        }
        throw new IOException(acknowledged
            ? "The gateway acknowledged activation, but Telnet port 23 is unavailable. Check firewall rules and gateway firmware."
            : "Telnet activation was not confirmed and port 23 is unavailable. Verify the gateway key, firmware support and firewall rules before retrying.");
    }

    internal static string CreatePassword(uint did, string mac, string key)
    {
        var secret = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(did.ToString(CultureInfo.InvariantCulture) + mac + key))).ToLowerInvariant();
        var digest = HMACSHA256.HashData(Encoding.ASCII.GetBytes(secret), Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(digest)[^16..];
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException("miIO.info is missing " + property + ". Telnet activation stopped.");
        return value.GetString()!;
    }

    private static async Task<bool> IsOpenAsync(string host, CancellationToken token)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await client.ConnectAsync(host, 23, timeout.Token);
            return true;
        }
        catch (SocketException)
        {
            token.ThrowIfCancellationRequested();
            return false;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
    }
}
