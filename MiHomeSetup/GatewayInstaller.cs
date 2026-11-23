using System.Security.Cryptography;

namespace MiHomeSetup;

internal static class GatewayInstaller
{
    internal const string AgentArguments = "miio mqtt cache central z3 --zigbee.tcp=8888";
    // Leave room for shell quoting, the upload path and both result markers within a 1024-byte input line.
    internal const int UploadChunkSize = 480;
    // Read full arguments from /proc because the gateway's ps output may truncate them.
    private const string RunningAgentsCommand =
        "for p in /proc/[0-9]*/exe; do " +
        "case \"$(readlink \"$p\" 2>/dev/null)\" in */openmiio_agent|*/openmiio_agent\\ \\(deleted\\)) " +
        "tr '\\000' ' ' < \"${p%/exe}/cmdline\"; printf '\\n';; esac; done";

    internal static async Task<byte[]> DownloadAsync(GatewayProfile profile, CancellationToken token, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        var url = "https://github.com/AlexxIT/openmiio_agent/releases/download/v1.2.1/openmiio_agent_" + profile.Architecture;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        output.WriteLine("Downloading openmiio_agent v1.2.1 (" + profile.Architecture + ")...");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var downloadBuffer = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (downloadBuffer.Length + count > 16 * 1024 * 1024) throw new InvalidDataException("Agent exceeds 16 MiB.");
            downloadBuffer.Write(buffer, 0, count);
        }
        var binary = downloadBuffer.ToArray();
        ValidateBinary(binary, profile);
        return binary;
    }

    internal static void ValidateBinary(byte[] binary, GatewayProfile profile)
    {
        // Pinned upstream fingerprint from XiaomiGateway3 core/shell/const.py, fetched over HTTPS.
        if (binary.Length < 1024 || !Convert.ToHexString(MD5.HashData(binary)).Equals(profile.AgentMd5, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Agent does not match the pinned upstream fingerprint.");
    }

    internal static async Task VerifyAsync(IGatewayShell shell, GatewayProfile profile, string model, CancellationToken token, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        var actualModel = await shell.RunAsync(profile.ModelCommand, token);
        if (GatewayModelDetector.ParseShellModel(actualModel, profile.Architecture) != model)
            throw new InvalidOperationException("Actual gateway model could not be verified. Installation stopped.");
        var architecture = await shell.RunAsync("uname -m", token);
        if (profile.Architecture == "mips" ? !architecture.StartsWith("mips", StringComparison.Ordinal)
            : !architecture.StartsWith("arm", StringComparison.Ordinal))
            throw new InvalidOperationException("Gateway CPU does not match the selected binary.");
        output.WriteLine("Verified model: " + model + "; CPU: " + architecture);
        var firmware = await shell.RunAsync(profile.Architecture == "mips"
            ? "cat /etc/rootfs_fw_info" : "agetprop ro.sys.mi_fw_ver; agetprop ro.sys.mi_build_num", token);
        output.WriteLine("Firmware: " + firmware.Replace('\n', ' '));
    }

    internal static async Task InstallAsync(IGatewayShell shell, GatewayProfile profile, Func<CancellationToken, Task<bool>> check, CancellationToken token, TimeSpan? retryDelay = null,
        Func<CancellationToken, Task<byte[]>>? download = null, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= TextWriter.Null;
        error ??= TextWriter.Null;
        var installed = await shell.RunAsync("if [ -x /data/openmiio_agent ]; then md5sum /data/openmiio_agent; fi", token);
        var replace = !installed.StartsWith(profile.AgentMd5 + " ", StringComparison.OrdinalIgnoreCase);
        var running = await shell.RunAsync(RunningAgentsCommand, token);
        if (!replace && IsCompatibleAgent(running) && await check(token))
        {
            output.WriteLine("The XiaomiGateway3-compatible agent is already running. Preparation is ready.");
            return;
        }
        var temporaryPath = "/data/openmiio_agent.mihome-" + Guid.NewGuid().ToString("N");
        var backup = temporaryPath + ".backup";
        var hadPrevious = false;
        var servicesStopped = false;
        var replaced = false;
        try
        {
            if (replace)
            {
                var binary = await (download is null ? DownloadAsync(profile, token, output) : download(token));
                ValidateBinary(binary, profile);
                await shell.RunAsync(": > " + temporaryPath, token);
                for (var offset = 0; offset < binary.Length; offset += UploadChunkSize)
                {
                    var chunk = Convert.ToBase64String(binary, offset, Math.Min(UploadChunkSize, binary.Length - offset));
                    await shell.RunAsync("echo '" + chunk + "' | base64 -d >> " + temporaryPath, token);
                }
                var checksum = await shell.RunAsync("md5sum " + temporaryPath, token);
                if (!checksum.StartsWith(profile.AgentMd5 + " ", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Uploaded agent checksum mismatch.");
                await shell.RunAsync("chmod 755 " + temporaryPath, token);
                hadPrevious = await shell.RunAsync("if [ -f /data/openmiio_agent ]; then echo yes; fi", token) == "yes";
                if (hadPrevious) await shell.RunAsync("cp /data/openmiio_agent " + backup, token);
            }
            output.WriteLine("Preparing the XiaomiGateway3-compatible agent. Existing MQTT clients may disconnect.");
            servicesStopped = true;
            await shell.RunAsync("killall openmiio_agent 2>/dev/null; true", token);
            if (replace)
            {
                // Mark first, so recovery also runs if the acknowledgement is lost after mv.
                replaced = true;
                await shell.RunAsync("mv " + temporaryPath + " /data/openmiio_agent", token);
            }
            // The agent prepares the stock broker or a patched /tmp/mosquitto for this firmware.
            await StartAsync(shell, token);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(retryDelay ?? TimeSpan.FromSeconds(2), token);
                if (await check(token))
                {
                    output.WriteLine("Setup complete. The agent path, version and startup options match XiaomiGateway3.");
                    if (hadPrevious) output.WriteLine("Previous agent retained at " + backup);
                    return;
                }
            }
            throw new IOException("Post-installation checks failed. Agent log: /var/log/openmiio.log.");
        }
        catch
        {
            if (servicesStopped)
            {
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await shell.RunAsync("killall openmiio_agent 2>/dev/null; killall mosquitto 2>/dev/null; true", recovery.Token);
                    if (replaced)
                        await shell.RunAsync(hadPrevious ? "mv " + backup + " /data/openmiio_agent" : "rm -f /data/openmiio_agent", recovery.Token);
                    if (!replace || hadPrevious) await StartAsync(shell, recovery.Token);
                    else await shell.RunAsync("mosquitto -d", recovery.Token);
                    error.WriteLine("Recovery attempted. Run check to inspect the current gateway state.");
                }
                catch (Exception exception)
                {
                    error.WriteLine("Automatic recovery failed: " + exception.Message + ". Backup, if created: " + backup);
                }
            }
            throw;
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await shell.RunAsync("rm -f " + temporaryPath, cleanup.Token); }
            catch (Exception) { error.WriteLine("Could not remove temporary upload: " + temporaryPath); }
        }
    }

    internal static bool IsCompatibleAgent(string running)
    {
        var processes = running.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (processes.Length != 1) return false;
        var arguments = processes[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return arguments.Length > 0 && arguments[0] == "/data/openmiio_agent" &&
            AgentArguments.Split(' ').All(required => arguments.Contains(required, StringComparer.Ordinal));
    }

    private static Task<string> StartAsync(IGatewayShell shell, CancellationToken token) =>
        shell.RunAsync("/data/openmiio_agent " + AgentArguments + " > /var/log/openmiio.log 2>&1 & true", token);
}
