using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Data.Sqlite;
using MiHomeLib.Contracts;

namespace MiHomeLib.MqttGateway;

internal class CommonDevicesDiscoverer(string host, int port, string login, string endMarker, string zigbeePath, string blePath) : IDevicesDiscoverer
{
    private const int READ_DELAY_MS = 100;
    // Give up when the gateway sends nothing for this long; any received data restarts the wait
    private static readonly TimeSpan SILENCE_TIMEOUT = TimeSpan.FromSeconds(10);
    private const string END_LINE = "\r\n";
    private const string PROMPT = ": ";
    private string ReadUntil(NetworkStream stream, string endMarker, string failString = "Password: ")
    {
        var sb = new StringBuilder();
        var deadline = DateTime.UtcNow + SILENCE_TIMEOUT;

        do
        {
            while (stream.DataAvailable)
            {
                var resp = Encoding.ASCII.GetString(ReceiveBytes(stream));
                sb.Append(resp);
                deadline = DateTime.UtcNow + SILENCE_TIMEOUT;
                if (resp.EndsWith(endMarker) || resp.EndsWith(failString))
                    return sb
                            .ToString()
                            .TrimEnd(endMarker.ToCharArray())
                            .TrimEnd(END_LINE.ToCharArray());
            }
            Thread.Sleep(READ_DELAY_MS);
        } while (DateTime.UtcNow < deadline);

        throw new Exception($"No data ending with marker '{endMarker}' detected within {SILENCE_TIMEOUT.TotalSeconds} seconds");
    }
    public byte[] ReceiveBytes(NetworkStream stream, int bufferSize = 4096)
    {
        var buffer = new byte[bufferSize];

        var recevied = stream.Read(buffer, 0, buffer.Length);

        if (recevied > bufferSize)
        {
            throw new Exception("Data received is greater than buffer size");
        }

        return new ArraySegment<byte>(buffer, 0, recevied).ToArray();
    }
    private void WriteStringToStream(NetworkStream stream, string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s + END_LINE);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
    public byte[] ReadFileByPath(string path)
    {
        using var stream = new TcpClient(host, port).GetStream();

        ReadUntil(stream, PROMPT);
        WriteStringToStream(stream, login);
        var result = ReadUntil(stream, endMarker);
        WriteStringToStream(stream, "\r\n");
        ReadUntil(stream, endMarker);

        WriteStringToStream(stream, $"ls {path}");

        if (ReadUntil(stream, endMarker).EndsWith("No such file or directory"))
        {
            throw new Exception($"Looks like file '{path}' is not found on your device");
        }

        var cmd = $"cat {path} | base64";

        WriteStringToStream(stream, cmd);

        var data = ReadUntil(stream, endMarker)
            .Replace(cmd + END_LINE, string.Empty)
            .Replace(END_LINE, string.Empty);

        stream.Close();

        return Convert.FromBase64String(data);
    }
    // BLE and mesh devices live in the same database. The copy downloaded for BLE discovery is kept
    // only for the mesh discovery that follows it, so a later discovery run still sees a fresh database
    private byte[] _bleDbForMesh;

    public List<BleDeviceInfo> DiscoverBleDevices()
    {
        var db = ReadFileByPath(blePath);
        _bleDbForMesh = db;

        return QueryBleDb(db, "SELECT mac, pid, did FROM gateway_authed_table",
            reader => new BleDeviceInfo(reader.GetString(2), reader.GetInt32(1), reader.GetString(0)));
    }
    public List<MeshDeviceInfo> DiscoverMeshDevices()
    {
        var db = _bleDbForMesh ?? ReadFileByPath(blePath);
        _bleDbForMesh = null;

        // Older firmware has no mesh table, which just means there are no mesh devices
        try
        {
            return QueryBleDb(db, "SELECT did, pdid, mac FROM mesh_device_v3",
                reader => new MeshDeviceInfo(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
        }
        catch (SqliteException e) when (e.Message.Contains("no such table"))
        {
            return [];
        }
    }
    private static List<T> QueryBleDb<T>(byte[] db, string sql, Func<SqliteDataReader, T> map)
    {
        var tmpName = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(tmpName, db);

            // Pooling=False releases the file handle on dispose, otherwise the temp file can't be deleted on Windows
            using var conn = new SqliteConnection($"Data Source={tmpName};Pooling=False");
            conn.Open();

            using var command = conn.CreateCommand();
            command.CommandText = sql;

            using var reader = command.ExecuteReader();
            var result = new List<T>();

            while (reader.Read()) result.Add(map(reader));

            return result;
        }
        finally
        {
            File.Delete(tmpName);
        }
    }
    public List<ZigBeeDeviceInfo> DiscoverZigBeeDevices()
    {
        var json = JsonNode.Parse(Encoding.ASCII.GetString(ReadFileByPath(zigbeePath)));

        return [.. json["devInfo"]
                .AsArray()
                .Select(x => new ZigBeeDeviceInfo(x["did"].ToString(), x["model"].ToString()))];
    }
}
