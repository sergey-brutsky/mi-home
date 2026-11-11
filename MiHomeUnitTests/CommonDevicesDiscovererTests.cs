using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests;

public class CommonDevicesDiscovererTests
{
    private const string ZIGBEE_PATH = "/data/zigbee/device.info";
    private const string BLE_PATH = "/data/miio/mible_local.db";

    /// <summary>
    /// Builds mible_local.db with the same tables the gateway has: one BLE and one mesh device
    /// </summary>
    private static byte[] BuildMibleDb(bool withMeshTable = true)
    {
        var path = Path.GetTempFileName();

        try
        {
            using (var conn = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "CREATE TABLE gateway_authed_table(macpid CHAR(16) PRIMARY KEY NOT NULL, mac CHAR(12) NOT NULL, pid INTEGER NOT NULL, token CHAR(24) NOT NULL, did CHAR(20) NOT NULL, beaconkey CHAR(40) NOT NULL);" +
                    "INSERT INTO gateway_authed_table VALUES('k', '79e0fe38c1a4', 1371, 't', 'blt.3.ble', 'b');" +
                    (withMeshTable
                        ? "CREATE TABLE mesh_device_v3(did CHAR(20) PRIMARY KEY, mac CHAR(12), pdid INTEGER, uni_addr INTEGER);" +
                          "INSERT INTO mesh_device_v3 VALUES('1063744008', 'aa:bb:cc:dd:ee:ff', 1945, 13);"
                        : "");
                cmd.ExecuteNonQuery();
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildMibleDbWithManyBleDevices(int deviceCount)
    {
        var path = Path.GetTempFileName();

        try
        {
            using (var conn = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                conn.Open();
                using (var create = conn.CreateCommand())
                {
                    create.CommandText =
                        "CREATE TABLE gateway_authed_table(macpid CHAR(16) PRIMARY KEY NOT NULL, mac CHAR(12) NOT NULL, pid INTEGER NOT NULL, token CHAR(24) NOT NULL, did CHAR(20) NOT NULL, beaconkey CHAR(40) NOT NULL);";
                    create.ExecuteNonQuery();
                }

                for (var i = 0; i < deviceCount; i++)
                {
                    using var insert = conn.CreateCommand();
                    insert.CommandText = "INSERT INTO gateway_authed_table VALUES($macpid, $mac, 1371, $token, $did, 'b');";
                    insert.Parameters.AddWithValue("$macpid", $"key-{i:D4}");
                    insert.Parameters.AddWithValue("$mac", "79e0fe38c1a4");
                    insert.Parameters.AddWithValue("$token", i == 0 ? new string('x', 10_000) : "t");
                    insert.Parameters.AddWithValue("$did", $"blt.3.device{i:D4}");
                    insert.ExecuteNonQuery();
                }
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Ble_And_Mesh_Discovery_Download_Database_Once()
    {
        // Arrange
        using var server = new FakeTelnetServer(new() { [BLE_PATH] = BuildMibleDb() });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, BLE_PATH);

        // Act
        var ble = discoverer.DiscoverBleDevices();
        var mesh = discoverer.DiscoverMeshDevices();

        // Assert
        ble.Should().ContainSingle();
        ble[0].Did.Should().Be("blt.3.ble");
        ble[0].Pdid.Should().Be(1371);
        ble[0].Mac.Should().Be("79e0fe38c1a4");

        mesh.Should().ContainSingle();
        mesh[0].Did.Should().Be("1063744008");
        mesh[0].Pdid.Should().Be(1945);
        mesh[0].Mac.Should().Be("aa:bb:cc:dd:ee:ff");

        server.CatCount.Should().Be(1, "BLE and mesh devices live in the same mible_local.db");
    }

    [Fact]
    public void Mesh_Discovery_Without_Mesh_Table_Returns_Empty_List()
    {
        // Arrange: older firmware has no mesh_device_v3
        using var server = new FakeTelnetServer(new() { [BLE_PATH] = BuildMibleDb(withMeshTable: false) });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, BLE_PATH);

        // Act & Assert
        discoverer.DiscoverMeshDevices().Should().BeEmpty();
        discoverer.DiscoverBleDevices().Should().ContainSingle();
    }

    [Fact]
    public void Ble_Discovery_Reads_Multiple_Btree_Pages_And_Overflow_Payloads()
    {
        using var server = new FakeTelnetServer(new() { [BLE_PATH] = BuildMibleDbWithManyBleDevices(300) });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, BLE_PATH);

        var devices = discoverer.DiscoverBleDevices();

        devices.Should().HaveCount(300);
        devices[0].Did.Should().Be("blt.3.device0000");
        devices[^1].Did.Should().Be("blt.3.device0299");
    }

    [Fact]
    public async Task Silent_Gateway_Fails_With_Timeout_Instead_Of_Hanging()
    {
        // Arrange: gateway accepts the connection but never prints a prompt
        using var server = new FakeTelnetServer([], silent: true);
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, "");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var discovery = Task.Run(() => discoverer.DiscoverZigBeeDevices());
        var finished = await Task.WhenAny(discovery, Task.Delay(TimeSpan.FromSeconds(30))) == discovery;

        // Assert: fails after ~10 seconds of silence
        finished.Should().BeTrue("discovery must not wait for the gateway forever");
        await discovery.Invoking(x => x).Should().ThrowAsync<Exception>().WithMessage("*marker*");
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(9)).And.BeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void ZigBee_Devices_Are_Read_From_Device_Info()
    {
        // Arrange
        var deviceInfo = "{\"devInfo\":[{\"did\":\"lumi.158d000aaa\",\"model\":\"lumi.sensor_ht\"}]}";
        using var server = new FakeTelnetServer(new() { [ZIGBEE_PATH] = Encoding.ASCII.GetBytes(deviceInfo) });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, "");

        // Act
        var devices = discoverer.DiscoverZigBeeDevices();

        // Assert
        devices.Should().ContainSingle();
        devices[0].Did.Should().Be("lumi.158d000aaa");
        devices[0].Model.Should().Be("lumi.sensor_ht");
    }
}
