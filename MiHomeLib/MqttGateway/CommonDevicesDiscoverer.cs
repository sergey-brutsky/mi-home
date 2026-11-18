using MiHomeLib.MqttGateway.Discovery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.MqttGateway;

internal sealed class CommonDevicesDiscoverer(
    string host,
    int port,
    string login,
    string endMarker,
    string zigbeePath,
    string blePath,
    TimeSpan? silenceTimeout = null) : IDevicesDiscoverer, IAsyncDevicesDiscoverer
{
    private readonly GatewayFileReader _files = new(host, port, login, endMarker, silenceTimeout ?? TimeSpan.FromSeconds(10));
    // BLE and mesh devices live in the same database. The copy downloaded for BLE discovery is kept
    // only for the mesh discovery that follows it, so a later discovery run still sees a fresh database
    private byte[] _bleDbForMesh;

    public List<BleDeviceInfo> DiscoverBleDevices() => DiscoverBleDevicesAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task<List<BleDeviceInfo>> DiscoverBleDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = await _files.ReadFileAsync(blePath, cancellationToken).ConfigureAwait(false);
        _bleDbForMesh = db;

        if (!SqliteTableReader.TryReadTable(db, "gateway_authed_table", ["mac", "pid", "did"], out var rows))
            throw new InvalidDataException("The SQLite database does not contain the 'gateway_authed_table' table.");

        return rows.ConvertAll(row => new BleDeviceInfo(
            GetString(row[2], "did"),
            GetInt32(row[1], "pid"),
            GetString(row[0], "mac")));
    }
    public List<MeshDeviceInfo> DiscoverMeshDevices() => DiscoverMeshDevicesAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task<List<MeshDeviceInfo>> DiscoverMeshDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = _bleDbForMesh ?? await _files.ReadFileAsync(blePath, cancellationToken).ConfigureAwait(false);
        _bleDbForMesh = null;

        // Older firmware has no mesh table, which just means there are no mesh devices
        if (!SqliteTableReader.TryReadTable(db, "mesh_device_v3", ["did", "pdid", "mac"], out var rows))
            return [];

        return rows.ConvertAll(row => new MeshDeviceInfo(
            GetString(row[0], "did"),
            GetInt32(row[1], "pdid"),
            GetString(row[2], "mac")));
    }

    private static string GetString(object value, string columnName) =>
        value as string ?? throw new InvalidDataException($"SQLite column '{columnName}' is not text.");

    private static int GetInt32(object value, string columnName)
    {
        if (value is not long number || number < int.MinValue || number > int.MaxValue)
            throw new InvalidDataException($"SQLite column '{columnName}' is not a valid 32-bit integer.");
        return (int)number;
    }
    public List<ZigBeeDeviceInfo> DiscoverZigBeeDevices() => DiscoverZigBeeDevicesAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task<List<ZigBeeDeviceInfo>> DiscoverZigBeeDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonNode.Parse(Encoding.ASCII.GetString(await _files.ReadFileAsync(zigbeePath, cancellationToken).ConfigureAwait(false)));

        return [.. json["devInfo"]
                .AsArray()
                .Select(x => new ZigBeeDeviceInfo(x["did"].ToString(), x["model"].ToString()))];
    }
}
