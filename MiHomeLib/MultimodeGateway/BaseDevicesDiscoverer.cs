using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Data.Sqlite;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway;

internal class MultimodeGateway2DevicesDiscoverer(string host, int port) :
    BaseDevicesDiscoverer(host, port, "root", "Mijia_Hub_V2-BC63 login: ", "/ # ", "/data/local/miio_bt/mible_local.db")
{ }

internal class MultimodeGatewayDevicesDiscoverer(string host, int port) :
    BaseDevicesDiscoverer(host, port, "admin", "rlxlinux login: ", "# ", "/data/miio/mible_local.db")
{ }

internal abstract class BaseDevicesDiscoverer(
    string host,
    int port,
    string username,
    string prompt,
    string endMarker,
    string pathToBleDB
) : IDevicesDiscoverer
{
    private const int READ_DELAY_MS = 100;
    private const int MAX_READ_ATTEMPTS = 100;
    private const string END_LINE = "\r\n";
    private const string ZIGBEE_DEVICES_PATH = "/data/zigbee/device.info";
    private readonly string _username = username;
    private readonly string _prompt = prompt;
    private readonly string _endMarker = endMarker;
    private readonly string _pathToBleDB = pathToBleDB;
    private string ReadUntil(NetworkStream stream, string endMarker)
    {
        var sb = new StringBuilder();
        var attempt = 0;

        do
        {
            while (stream.DataAvailable)
            {
                var resp = Encoding.ASCII.GetString(ReceiveBytes(stream));
                sb.Append(resp);
                if (resp.EndsWith(endMarker))
                    return sb
                            .ToString()
                            .TrimEnd(_endMarker.ToCharArray())
                            .TrimEnd(END_LINE.ToCharArray());
            }
            Thread.Sleep(READ_DELAY_MS);
        } while (attempt < MAX_READ_ATTEMPTS);

        throw new Exception($"No data ending with marker '{endMarker}' detected");
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

        ReadUntil(stream, _prompt);
        WriteStringToStream(stream, _username);
        ReadUntil(stream, _endMarker);
        WriteStringToStream(stream, $"ls {path}");

        if (ReadUntil(stream, _endMarker).EndsWith("No such file or directory"))
        {
            throw new Exception($"Looks like file '{path}' is not found on your device");
        }

        var cmd = $"cat {path} | base64";

        WriteStringToStream(stream, cmd);

        var data = ReadUntil(stream, _endMarker)
            .Replace(cmd + END_LINE, string.Empty)
            .Replace(END_LINE, string.Empty);

        stream.Close();

        return Convert.FromBase64String(data);
    }
    public List<(string did, int pdid, string mac)> DiscoverBleDevices()
    {
        var bleDevicesList = new List<(string did, int pdid, string mac)>();

        var tmpName = Path.GetTempFileName();
        File.WriteAllBytes(tmpName, ReadFileByPath(_pathToBleDB));

        using var conn = new SqliteConnection($"Data Source={tmpName}");
        conn.Open();

        var command = conn.CreateCommand();
        command.CommandText = "SELECT mac, pid, did FROM gateway_authed_table";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var mac = reader.GetString(0);
            var pdid = reader.GetInt32(1);
            var did = reader.GetString(2);
            bleDevicesList.Add((did, pdid, mac));
        }

        File.Delete(tmpName);

        return bleDevicesList;
    }
    public List<(string did, string model)> DiscoverZigBeeDevices()
    {
        var json = JsonNode.Parse(Encoding.ASCII.GetString(ReadFileByPath(ZIGBEE_DEVICES_PATH)));

        return [.. json["devInfo"]
                .AsArray()
                .Select(x => (x["did"].ToString(), x["model"].ToString()))];
    }
}
