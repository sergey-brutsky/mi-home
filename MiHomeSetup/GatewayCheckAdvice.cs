using System.Net.Sockets;
using System.Text.Json;

namespace MiHomeSetup;

internal static class GatewayCheckAdvice
{
    internal static void Write(SetupOptions options, GatewayReadiness readiness, GatewayInfo info, TextWriter output)
    {
        if (readiness.Ready) return;
        output.WriteLine();
        if (options.Model != info.Model)
        {
            output.WriteLine("The detected model differs from the requested model. Run check again without model= to select the correct gateway profile.");
            return;
        }
        if (!readiness.Discovery && !ConnectionUnavailable(readiness.DiscoveryError))
        {
            output.WriteLine("Automatic setup cannot repair the discovery error reported above.");
            output.WriteLine("Check the gateway's passwordless Telnet login and discovery files using its wiki instructions.");
            output.WriteLine("https://github.com/sergey-brutsky/mi-home/wiki");
            return;
        }
        var needsKey = false;
        if (!readiness.Discovery)
        {
            if (string.IsNullOrWhiteSpace(info.Firmware))
            {
                output.WriteLine("Automatic setup eligibility is unknown because the firmware version could not be read.");
                output.WriteLine("To identify the firmware, run check with the gateway's miIO token.");
                output.WriteLine("  make gw check " + options.Target + " model=" + info.Model + " token=\"<MIIO_TOKEN>\"");
                output.WriteLine("If miIO remains unavailable, verify the token, gateway IP and access to UDP port 54321.");
                return;
            }
            output.WriteLine("Firmware: " + info.Firmware);
            string method;
            try { method = GatewayFirmware.Parse(info.Firmware).TelnetMethod(info.Model); }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
            {
                output.WriteLine("Automatic Telnet setup is unavailable for this firmware under the current compatibility rules.");
                output.WriteLine("Enable Telnet manually using the gateway's wiki instructions, then rerun check.");
                output.WriteLine("https://github.com/sergey-brutsky/mi-home/wiki");
                return;
            }
            needsKey = method == "system_command";
            if (needsKey)
                output.WriteLine(options.Key is not null || options.KeyFile is not null
                    ? "A gateway key was provided. Setup will verify it while enabling Telnet."
                    : "Automatic setup requires the separate 16-character gateway key saved from XiaomiGateway3.");
            else output.WriteLine("Automatic setup is supported for this model and firmware using its miIO token.");
        }
        else output.WriteLine("Device discovery is working. Setup can prepare the MQTT service using the existing Telnet access.");

        var command = "make gw setup " + options.Target + " model=" + info.Model;
        if (!readiness.Discovery)
        {
            command += options.TokenFile is not null
                ? " token_file=" + Quote(options.TokenFile)
                : " token=\"<MIIO_TOKEN>\"";
            if (needsKey)
                command += options.KeyFile is not null ? " key_file=" + Quote(options.KeyFile)
                    : " key=\"<KEY FROM GW>\"";
        }
        output.WriteLine("Run setup on this gateway.");
        output.WriteLine("  " + command);
        output.WriteLine("Setup will verify access, prepare MQTT and check the result on the gateway.");
        output.WriteLine("After setup, verify readiness.");
        output.WriteLine("  make gw check " + options.Target);
    }

    internal static string DescribeError(Exception exception, int port)
    {
        if (exception.GetBaseException() is SocketException socket && socket.SocketErrorCode == SocketError.ConnectionRefused)
            return "Port " + port + " is closed or rejecting connections.";
        if (ConnectionUnavailable(exception)) return "No connection on port " + port + ". Check network access and firewall rules.";
        if (exception is UnauthorizedAccessException) return "Telnet requires a password; passwordless administrative access is needed.";
        if (exception is FileNotFoundException) return "A gateway discovery file is missing.";
        if (exception is InvalidDataException or JsonException) return "Gateway discovery data could not be parsed.";
        return "The connection or protocol check failed on port " + port + ".";
    }

    private static bool ConnectionUnavailable(Exception? exception) =>
        exception is TimeoutException || exception?.GetBaseException() is SocketException;

    private static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
}
