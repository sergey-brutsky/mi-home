using System.Net.Sockets;
using System.Text;

namespace MiHomeSetup;

internal sealed class GatewayShell : IGatewayShell, IDisposable
{
    private readonly TcpClient _client = new();
    private readonly TimeSpan _timeout;
    private NetworkStream _stream = null!;
    private string _prompt = "# ";
    private int _telnetState;
    private byte _telnetCommand;

    internal GatewayShell(TimeSpan? timeout = null) => _timeout = timeout ?? TimeSpan.FromSeconds(20);

    internal async Task<GatewayProfile> ConnectAsync(string host, GatewayProfile? profile, CancellationToken token, int port = 23)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_timeout);
        await _client.ConnectAsync(host, port, timeout.Token);
        _stream = _client.GetStream();
        var banner = await ReadUntilAsync(text => text.EndsWith(": ", StringComparison.Ordinal), timeout.Token);
        profile ??= GatewayProfile.FromTelnetBanner(banner);
        await WriteAsync(profile.Login, timeout.Token);
        await ReadUntilAsync(text => text.EndsWith(profile.Prompt, StringComparison.Ordinal), timeout.Token);
        _prompt = profile.Prompt;
        return profile;
    }

    public async Task<string> RunAsync(string command, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_timeout);
        var marker = Guid.NewGuid().ToString("N");
        await WriteAsync(BuildCommand(command, marker), timeout.Token);
        var response = await ReadUntilAsync(text => text.EndsWith(_prompt, StringComparison.Ordinal) &&
            text.Contains("\nMIHOME_" + marker + "_END:", StringComparison.Ordinal), timeout.Token);
        return ParseResponse(response, marker);
    }

    internal static string BuildCommand(string command, string marker) =>
        // Split marker arguments prevent terminal echo, including wrapped echo, from becoming a result delimiter.
        "printf '\\n%s%s\\n' 'MIHOME_' '" + marker + "_BEGIN'; { " + command +
        "; }; printf '\\n%s%s:%s\\n' 'MIHOME_' '" + marker + "_END' \"$?\"";

    internal static string ParseResponse(string response, string marker)
    {
        var text = response.Replace("\r\n", "\n").Replace('\r', '\n');
        var begin = "\nMIHOME_" + marker + "_BEGIN\n";
        var end = "\nMIHOME_" + marker + "_END:";
        var start = text.IndexOf(begin, StringComparison.Ordinal);
        var finish = text.LastIndexOf(end, StringComparison.Ordinal);
        if (start < 0 || finish < start + begin.Length)
            throw new InvalidDataException("Gateway command response is missing its result markers.");
        var statusStart = finish + end.Length;
        var statusEnd = text.IndexOf('\n', statusStart);
        if (statusEnd < 0)
            throw new InvalidDataException("Gateway command response is missing its exit status.");
        var status = text[statusStart..statusEnd].Trim();
        if (!int.TryParse(status, out var exitCode))
            throw new InvalidDataException("Gateway command response has an invalid exit status.");
        if (exitCode != 0) throw new IOException("Gateway command failed (exit " + status + ").");
        return text[(start + begin.Length)..finish].Trim();
    }

    private Task WriteAsync(string text, CancellationToken token) =>
        _stream.WriteAsync(Encoding.ASCII.GetBytes(text + "\r\n"), token).AsTask();

    private async Task<string> ReadUntilAsync(Func<string, bool> complete, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new byte[4096];
        while (result.Length < 1024 * 1024)
        {
            var count = await _stream.ReadAsync(buffer, token);
            if (count == 0) throw new EndOfStreamException("Gateway closed the administrative connection.");
            for (var i = 0; i < count; i++)
                await AppendTelnetByteAsync(buffer[i], result, token);
            var text = result.ToString();
            if (text.EndsWith("Password: ", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Telnet requires a password. MiHomeLib discovery supports passwordless login only.");
            if (complete(text)) return text;
        }
        throw new InvalidDataException("Gateway response exceeded 1 MiB.");
    }

    private async Task AppendTelnetByteAsync(byte value, StringBuilder text, CancellationToken token)
    {
        // Refuse optional Telnet negotiation. Subnegotiation is ignored through IAC SE.
        switch (_telnetState)
        {
            case 0:
                if (value == 255) _telnetState = 1;
                else text.Append((char)value);
                break;
            case 1:
                if (value is 251 or 252 or 253 or 254) { _telnetCommand = value; _telnetState = 2; }
                else if (value == 250) _telnetState = 3;
                else { if (value == 255) text.Append((char)value); _telnetState = 0; }
                break;
            case 2:
                if (_telnetCommand is 251 or 253)
                    await _stream.WriteAsync(new byte[] { 255, _telnetCommand == 251 ? (byte)254 : (byte)252, value }, token);
                _telnetState = 0;
                break;
            case 3:
                if (value == 255) _telnetState = 4;
                break;
            case 4:
                _telnetState = value == 240 ? 0 : 3;
                break;
        }
    }

    public void Dispose() => _client.Dispose();
}
