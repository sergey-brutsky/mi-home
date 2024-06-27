using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace MiHomeLib.Transport;

internal class TelnetDevicesDiscoverer(string host, int port) : IDevicesDiscoverer
{
    private const int READ_DELAY_MS = 100;
    private const int MAX_READ_ATTEMPTS = 100;
    private const string END_MARKER = "# ";
    private const string END_LINE = "\r\n";
    private const string BLE_DEVICES_PATH = "/data/miio/mible_local.db";
    private const string ZIGBEE_DEVICES_PATH = "/data/zigbee/device.info";
    private string ReadUntil(NetworkStream stream, string endMarker)
    {
        var sb = new StringBuilder();
        var attempt = 0;

        do
        {
            while (stream.DataAvailable)
            {
                var resp = Encoding.ASCII.GetString(stream.ReceiveBytes());
                sb.Append(resp);
                if (resp.EndsWith(endMarker))
                    return sb
                            .ToString()
                            .TrimEnd(END_MARKER.ToCharArray())
                            .TrimEnd(END_LINE.ToCharArray());
            }
            Thread.Sleep(READ_DELAY_MS);
        } while (attempt < MAX_READ_ATTEMPTS);

        throw new Exception($"No data ending with marker '{endMarker}' detected");
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

        ReadUntil(stream, "rlxlinux login: ");
        WriteStringToStream(stream, "admin");
        ReadUntil(stream, END_MARKER);
        WriteStringToStream(stream, $"ls {path}");

        if (ReadUntil(stream, END_MARKER).EndsWith("No such file or directory"))
        {
            throw new Exception($"Looks like file '{path}' is not found on your device");
        }

        var cmd = $"cat {path} | base64";

        WriteStringToStream(stream, cmd);

        var data = ReadUntil(stream, END_MARKER)
            .Replace(cmd + END_LINE, string.Empty)
            .Replace(END_LINE, string.Empty);

        stream.Close();

        return Convert.FromBase64String(data);
    }
    public List<(string did, int pdid, string mac)> DiscoverBleDevices()
    {
        var bleDevicesList = new List<(string did, int pdid, string mac)>();

        var tmpName = Path.GetTempFileName();
        File.WriteAllBytes(tmpName, ReadFileByPath(BLE_DEVICES_PATH));

        using var conn = new SQLiteConnection($"Data Source={tmpName}");
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

        return json["devInfo"]
                .AsArray()
                .Select(x => (x["did"].ToString(), x["model"].ToString()))
                .ToList();
    }
}
