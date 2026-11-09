using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeUnitTests;

/// <summary>
/// Minimal telnet-like server for CommonDevicesDiscoverer tests: prints a login prompt,
/// then a shell prompt, answers `ls` and `cat ... | base64` from an in-memory file map
/// </summary>
internal sealed class FakeTelnetServer : IDisposable
{
    private const string PROMPT = "# ";
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, byte[]> _files;
    private readonly bool _silent;
    private int _catCount;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>
    /// How many times a file has been read with `cat`
    /// </summary>
    public int CatCount => Volatile.Read(ref _catCount);

    /// <param name="files">Files available on the fake gateway</param>
    /// <param name="silent">Accept the connection but never send anything</param>
    public FakeTelnetServer(Dictionary<string, byte[]> files, bool silent = false)
    {
        _files = files;
        _silent = silent;
        _listener.Start();
        Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(); }
            catch { return; }

            _ = Task.Run(() => Serve(client));
        }
    }

    private void Serve(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();

            if (_silent)
            {
                _cts.Token.WaitHandle.WaitOne();
                return;
            }

            Write(stream, "login: ");

            var buffer = new StringBuilder();
            var bytes = new byte[4096];

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var read = stream.Read(bytes, 0, bytes.Length);
                    if (read == 0) return;

                    buffer.Append(Encoding.ASCII.GetString(bytes, 0, read));

                    int idx;
                    while ((idx = buffer.ToString().IndexOf("\r\n", StringComparison.Ordinal)) >= 0)
                    {
                        var line = buffer.ToString(0, idx);
                        buffer.Remove(0, idx + 2);
                        Write(stream, Answer(line));
                    }
                }
            }
            catch (Exception) { } // client closed the connection
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

    private static void Write(NetworkStream stream, string s)
    {
        var data = Encoding.ASCII.GetBytes(s);
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
