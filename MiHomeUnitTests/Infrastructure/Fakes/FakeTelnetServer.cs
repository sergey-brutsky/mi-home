using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeUnitTests.Infrastructure.Fakes;

/// <summary>
/// Minimal telnet-like server for CommonDevicesDiscoverer tests: prints a login prompt,
/// then a shell prompt, answers `ls` and `cat ... | base64` from an in-memory file map
/// </summary>
internal sealed class FakeTelnetServer : IAsyncDisposable
{
    private const string PROMPT = "# ";
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, byte[]> _files;
    private readonly bool _silent;
    private readonly bool _splitPrompt;
    private readonly bool _password;
    private int _catCount;
    private readonly Task _acceptLoop;
    private readonly System.Collections.Concurrent.ConcurrentBag<Task> _clients = new();

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>
    /// How many times a file has been read with `cat`
    /// </summary>
    public int CatCount => Volatile.Read(ref _catCount);

    /// <param name="files">Files available on the fake gateway</param>
    /// <param name="silent">Accept the connection but never send anything</param>
    public FakeTelnetServer(Dictionary<string, byte[]> files, bool silent = false, bool splitPrompt = false, bool password = false)
    {
        _files = files;
        _silent = silent;
        _splitPrompt = splitPrompt;
        _password = password;
        _listener.Start();
        _acceptLoop = AcceptLoop();
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { return; }
            catch (SocketException) when (_cts.IsCancellationRequested) { return; }

            _clients.Add(Serve(client));
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        {
            using var cancellation = _cts.Token.Register(client.Dispose);
            try
            {
                var stream = client.GetStream();
                if (_silent)
                {
                    await Task.Delay(Timeout.Infinite, _cts.Token);
                    return;
                }

                await WriteAsync(stream, "login: ");
                var buffer = new StringBuilder();
                var bytes = new byte[4096];

                while (!_cts.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(bytes, _cts.Token);
                    if (read == 0) return;

                    buffer.Append(Encoding.ASCII.GetString(bytes, 0, read));

                    int idx;
                    while ((idx = buffer.ToString().IndexOf("\r\n", StringComparison.Ordinal)) >= 0)
                    {
                        var line = buffer.ToString(0, idx);
                        buffer.Remove(0, idx + 2);
                        await WriteAsync(stream, _password ? "Password: " : Answer(line));
                    }
                }
            }
            catch (System.IO.IOException error) when (
                _cts.IsCancellationRequested || error.InnerException is SocketException
                {
                    SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown
                }) { } // Expected peer disconnect; other IO errors must fail the test.
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested) { }
        }
    }

    private string Answer(string line)
    {
        if (line.StartsWith("ls "))
        {
            var path = line.Substring(3);
            return _files.ContainsKey(path)
                ? $"{line}\r\n{path}\r\n{PROMPT}"
                : $"{line}\r\nls: {path}: No such file or directory\r\n{PROMPT}";
        }

        if (line.StartsWith("cat ") && line.EndsWith(" | base64"))
        {
            Interlocked.Increment(ref _catCount);
            var path = line.Substring(4, line.Length - 4 - " | base64".Length);
            return $"{line}\r\n{Convert.ToBase64String(_files[path])}\r\n{PROMPT}";
        }

        // login name, empty line or anything else
        return $"{line}\r\n{PROMPT}";
    }

    private async Task WriteAsync(NetworkStream stream, string s)
    {
        var data = Encoding.ASCII.GetBytes(s);
        if (_splitPrompt && s.EndsWith(" "))
        {
            await stream.WriteAsync(data.AsMemory(0, data.Length - 1), _cts.Token);
            await Task.Delay(15, _cts.Token);
            await stream.WriteAsync(data.AsMemory(data.Length - 1, 1), _cts.Token);
        }
        else await stream.WriteAsync(data, _cts.Token);
        await stream.FlushAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            try { await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(2)); }
            finally { await Task.WhenAll(_clients).WaitAsync(TimeSpan.FromSeconds(2)); }
        }
        finally { _cts.Dispose(); }
    }
}
