using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.MqttGateway;

internal sealed class GatewayFileReader(string host, int port, string login, string endMarker, TimeSpan silenceTimeout)
{
    // Give up when the gateway sends nothing for this long; any received data restarts the wait
    private readonly TimeSpan _silenceTimeout = silenceTimeout;
    private const string END_LINE = "\r\n";
    private const string PROMPT = ": ";
    private async Task<string> ReadUntilAsync(NetworkStream stream, string endMarker, CancellationToken cancellationToken, string failString = "Password: ")
    {
        var response = new StringBuilder();
        var buffer = new byte[4096];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count;
            using (var silence = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (silence.Token.Register(stream.Dispose))
            {
                silence.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, _silenceTimeout.TotalMilliseconds)));
                try
                {
                    count = await stream.ReadAsync(buffer, 0, buffer.Length, silence.Token).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (silence.IsCancellationRequested)
                        throw new TimeoutException($"No data ending with marker '{endMarker}' detected within {_silenceTimeout.TotalSeconds} seconds");
                }
                catch (Exception exception) when (silence.IsCancellationRequested && exception is not TimeoutException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"No data ending with marker '{endMarker}' detected within {_silenceTimeout.TotalSeconds} seconds", exception);
                }
            }
            if (count == 0) throw new EndOfStreamException("Gateway closed the telnet connection.");
            response.Append(Encoding.ASCII.GetString(buffer, 0, count));
            if (EndsWith(response, failString))
                throw new UnauthorizedAccessException("Gateway telnet requires password authentication.");
            if (EndsWith(response, endMarker))
                return response.ToString(0, response.Length - endMarker.Length).TrimEnd('\r', '\n');
        }
    }

    private static bool EndsWith(StringBuilder text, string suffix)
    {
        if (text.Length < suffix.Length) return false;
        var offset = text.Length - suffix.Length;
        for (var i = 0; i < suffix.Length; i++)
            if (text[offset + i] != suffix[i]) return false;
        return true;
    }
    private async Task WriteStringToStreamAsync(NetworkStream stream, string s, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(s + END_LINE);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    public byte[] ReadFile(string path) => ReadFileAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    internal async Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var client = new TcpClient();
        using var cancellation = cancellationToken.Register(client.Dispose);
        try
        {
            await GatewayConnection.ConnectAsync(client, host, port, "telnet",
                " (see the gateway page in the project wiki)", cancellationToken).ConfigureAwait(false);
            using var stream = client.GetStream();
            await ReadUntilAsync(stream, PROMPT, cancellationToken).ConfigureAwait(false);
            await WriteStringToStreamAsync(stream, login, cancellationToken).ConfigureAwait(false);
            await ReadUntilAsync(stream, endMarker, cancellationToken).ConfigureAwait(false);
            await WriteStringToStreamAsync(stream, "", cancellationToken).ConfigureAwait(false);
            await ReadUntilAsync(stream, endMarker, cancellationToken).ConfigureAwait(false);
            await WriteStringToStreamAsync(stream, $"ls {path}", cancellationToken).ConfigureAwait(false);
            var listing = await ReadUntilAsync(stream, endMarker, cancellationToken).ConfigureAwait(false);
            if (listing.EndsWith("No such file or directory"))
                throw new FileNotFoundException($"Looks like file '{path}' is not found on your device");
            var command = $"cat {path} | base64";
            await WriteStringToStreamAsync(stream, command, cancellationToken).ConfigureAwait(false);
            var response = await ReadUntilAsync(stream, endMarker, cancellationToken).ConfigureAwait(false);
            var data = response.Replace(command + END_LINE, string.Empty).Replace(END_LINE, string.Empty);
            return Convert.FromBase64String(data);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
