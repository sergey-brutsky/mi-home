using MiHomeUnitTests.Infrastructure.Fakes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests.Discovery;

public class CommonDevicesDiscovererTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Nonpositive_Silence_Timeout_Retains_The_One_Millisecond_Minimum(int milliseconds)
    {
        await using var server = new FakeTelnetServer([], silent: true);
        var reader = new GatewayFileReader("127.0.0.1", server.Port, "admin", "# ", TimeSpan.FromMilliseconds(milliseconds));
        Func<Task> read = async () => await reader.ReadFileAsync("/data/test.db", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        await read.Should().ThrowAsync<TimeoutException>().WithMessage("*marker*");
    }

    [Fact]
    public async Task Closing_The_Connection_Reports_End_Of_Stream()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var reader = new GatewayFileReader("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "admin", "# ", TimeSpan.FromSeconds(1));
        var read = reader.ReadFileAsync("/data/test.db", CancellationToken.None);
        using var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(2));
        connection.Close();
        Func<Task> wait = async () => await read.WaitAsync(TimeSpan.FromSeconds(2));
        await wait.Should().ThrowAsync<EndOfStreamException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_File_Reports_The_Same_Error_In_Both_APIs(bool isAsync)
    {
        await using var server = new FakeTelnetServer([]);
        var reader = new GatewayFileReader("127.0.0.1", server.Port, "admin", "# ", TimeSpan.FromSeconds(1));
        Func<Task> read = () => isAsync
            ? reader.ReadFileAsync("/missing", CancellationToken.None)
            : Task.Run(() => reader.ReadFile("/missing"));
        await read.Should().ThrowAsync<FileNotFoundException>().WithMessage("*/missing*");
    }

    [Fact]
    public async Task File_Read_Can_Be_Cancelled_During_Silence()
    {
        await using var server = new FakeTelnetServer([], silent: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var reader = new GatewayFileReader("127.0.0.1", server.Port, "admin", "# ", TimeSpan.FromSeconds(5));
        Func<Task> read = async () => await reader.ReadFileAsync("/data/test.db", cancellation.Token).WaitAsync(TimeSpan.FromSeconds(2));
        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task File_Reader_Preserves_Large_Binary_Files_And_Fragmented_End_Markers(bool splitPrompt)
    {
        var expected = new byte[256 * 1024];
        new Random(123).NextBytes(expected);
        await using var server = new FakeTelnetServer(new() { ["/data/test.db"] = expected }, splitPrompt: splitPrompt);
        var reader = new GatewayFileReader("127.0.0.1", server.Port, "admin", "# ", TimeSpan.FromSeconds(2));

        var actual = reader.ReadFile("/data/test.db");

        actual.Should().Equal(expected);
        server.CatCount.Should().Be(1);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Split_Prompt_Is_Recognized(bool isAsync)
    {
        await using var server = new FakeTelnetServer(new() { [ZIGBEE_PATH] = Encoding.ASCII.GetBytes("{\"devInfo\":[]}") }, splitPrompt: true);
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, "", TimeSpan.FromMilliseconds(500));
        var devices = isAsync ? await discoverer.DiscoverZigBeeDevicesAsync(CancellationToken.None) : discoverer.DiscoverZigBeeDevices();
        devices.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Password_Request_Gives_Authentication_Error(bool isAsync)
    {
        await using var server = new FakeTelnetServer([], password: true);
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, "", TimeSpan.FromMilliseconds(500));
        Func<Task> discover = () => isAsync
            ? discoverer.DiscoverZigBeeDevicesAsync(CancellationToken.None)
            : Task.Run(discoverer.DiscoverZigBeeDevices);
        await discover.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ble_And_Mesh_Discovery_Download_Database_Once(bool isAsync)
    {
        // Arrange
        await using var server = new FakeTelnetServer(new() { [BLE_PATH] = BuildMibleDb() });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, BLE_PATH);

        // Act
        var ble = isAsync ? await discoverer.DiscoverBleDevicesAsync(CancellationToken.None) : discoverer.DiscoverBleDevices();
        var mesh = isAsync ? await discoverer.DiscoverMeshDevicesAsync(CancellationToken.None) : discoverer.DiscoverMeshDevices();

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
    public async Task Mesh_Discovery_Without_Mesh_Table_Returns_Empty_List()
    {
        // Arrange: older firmware has no mesh_device_v3
        await using var server = new FakeTelnetServer(new() { [BLE_PATH] = BuildMibleDb(withMeshTable: false) });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, BLE_PATH);

        // Act & Assert
        discoverer.DiscoverMeshDevices().Should().BeEmpty();
        discoverer.DiscoverBleDevices().Should().ContainSingle();
    }

    [Fact]
    public async Task Ble_Discovery_Reads_Multiple_Btree_Pages_And_Overflow_Payloads()
    {
        await using var server = new FakeTelnetServer(new() { [BLE_PATH] = BuildMibleDbWithManyBleDevices(300) });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, BLE_PATH);

        var devices = discoverer.DiscoverBleDevices();

        devices.Should().HaveCount(300);
        devices[0].Did.Should().Be("blt.3.device0000");
        devices[^1].Did.Should().Be("blt.3.device0299");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Silent_Gateway_Fails_With_Timeout_Instead_Of_Hanging(bool isAsync)
    {
        // Arrange: gateway accepts the connection but never prints a prompt
        await using var server = new FakeTelnetServer([], silent: true);
        var timeout = TimeSpan.FromMilliseconds(250);
        var discoverer = new CommonDevicesDiscoverer(
            "127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, "", timeout);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var discovery = isAsync ? discoverer.DiscoverZigBeeDevicesAsync(CancellationToken.None) : Task.Run(discoverer.DiscoverZigBeeDevices);
        var finished = await Task.WhenAny(discovery, Task.Delay(TimeSpan.FromSeconds(3))) == discovery;

        // Assert: fails after the shortened test-only timeout
        finished.Should().BeTrue("discovery must not wait for the gateway forever");
        await discovery.Invoking(x => x).Should().ThrowAsync<Exception>().WithMessage("*marker*");
        stopwatch.Elapsed.Should().BeGreaterThan(timeout).And.BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZigBee_Devices_Are_Read_From_Device_Info(bool isAsync)
    {
        // Arrange
        var deviceInfo = "{\"devInfo\":[{\"did\":\"lumi.158d000aaa\",\"model\":\"lumi.sensor_ht\"}]}";
        await using var server = new FakeTelnetServer(new() { [ZIGBEE_PATH] = Encoding.ASCII.GetBytes(deviceInfo) });
        var discoverer = new CommonDevicesDiscoverer("127.0.0.1", server.Port, "admin", "# ", ZIGBEE_PATH, "");

        // Act
        var devices = isAsync ? await discoverer.DiscoverZigBeeDevicesAsync(CancellationToken.None) : discoverer.DiscoverZigBeeDevices();

        // Assert
        devices.Should().ContainSingle();
        devices[0].Did.Should().Be("lumi.158d000aaa");
        devices[0].Model.Should().Be("lumi.sensor_ht");
    }
}
