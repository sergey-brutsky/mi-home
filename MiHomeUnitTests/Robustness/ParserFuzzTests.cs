using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using MiHomeLib.MqttGateway.ActionProcessors;
using MiHomeLib.Protocols;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Robustness;

public sealed class ParserFuzzTests
{
    [Fact]
    public void Gateway2_messages_survive_seeded_mutations_and_valid_fixture_still_parses()
    {
        var messages = LoadFixtureMessages("gateway2.jsonl");
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var input = SeededMutator.Mutate(messages[random.Next(messages.Count)], random);
            try { Gateway2Message.Parse(input); }
            catch (System.Text.Json.JsonException) { }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}: {exception}");
            }
        }

        Gateway2Message.Parse(messages[0]).Type.Should().Be(Gateway2MessageType.ReadAck);
    }

    [Fact]
    public void Mqtt_json_messages_survive_seeded_mutations()
    {
        var messages = LoadFixtureMessages("mqtt.jsonl");
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var input = SeededMutator.Mutate(messages[random.Next(messages.Count)], random);
            try
            {
                using var document = JsonDocument.Parse(input);
                MqttJson.Object(document.RootElement);
            }
            catch (JsonException) { }
            catch (FormatException) { }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}: {exception}");
            }
        }
    }

    [Fact]
    public void Gateway2_device_values_survive_mutations_and_valid_data_updates_device()
    {
        using var device = new XiaomiTemperatureHumiditySensor("sensor_ht", 1, NullLoggerFactory.Instance);
        device.InitializeProtocol(DeviceProtocol.XiaomiGateway2, "sensor_ht");
        var replacements = new[] { "null", "\"\"", "\"abc\"", "1e309", "-1", "2147483648", "[]", "{}", "true", "1.5" };
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var data = new JsonObject
            {
                ["temperature"] = JsonNode.Parse(replacements[random.Next(replacements.Length)]),
                ["humidity"] = JsonNode.Parse(replacements[random.Next(replacements.Length)]),
            };
            try { device.ParseData(data.ToJsonString()); }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}: {exception}");
            }
        }

        device.ParseData("{\"temperature\":\"2380\",\"humidity\":\"4120\"}");
        device.Temperature.Should().Be(23.8f);
        device.Humidity.Should().Be(41.2f);
    }

    private static readonly string[] ValueReplacements =
    [
        "null", "\"\"", "\"abc\"", "1e309", "-1", "2147483648", "[]", "{}", "true", "1.5",
        "0", "1", "\"1\"", "\"ff\"", "\"0102030405060708\"", "[{\"piid\":1,\"value\":1}]",
    ];

    public static IEnumerable<object[]> MappedDevices()
    {
        foreach (var entry in ProtocolDeviceCatalog.Entries)
        foreach (ProtocolDeviceModelAttribute metadata in entry.DeviceType.GetCustomAttributes(typeof(ProtocolDeviceModelAttribute), false))
            yield return [metadata.Protocol.ToString(), metadata.Model, metadata.Pdid];
    }

    [Theory]
    [MemberData(nameof(MappedDevices))]
    public void Every_mapped_device_value_survives_invalid_values(string protocolName, string model, int pdid)
    {
        var protocol = Enum.Parse<DeviceProtocol>(protocolName);
        using var device = CreateDevice(protocol, model, pdid);
        var keys = device.GetMappedKeys();
        keys.Should().NotBeEmpty();
        var random = new Random(SeededMutator.Seed);

        foreach (var key in keys)
        {
            foreach (var value in ValueReplacements)
                ParseWithoutException(device, protocol, key, value, $"{model}: '{key}' = {value}");

            // Seeded random numbers and hex-like strings on top of the fixed cases
            for (var iteration = 0; iteration < Math.Max(1, SeededMutator.Iterations / 100); iteration++)
            {
                var value = random.Next(3) switch
                {
                    0 => random.Next(int.MinValue, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    1 => JsonSerializer.Serialize(RandomHex(random)),
                    _ => JsonSerializer.Serialize(SeededMutator.RandomText(random, 20)),
                };
                ParseWithoutException(device, protocol, key, value,
                    $"seed {SeededMutator.Seed}, iteration {iteration}, {model}: '{key}' = {value}");
            }
        }
    }

    private static ProtocolMappedDeviceBase CreateDevice(DeviceProtocol protocol, string model, int pdid)
    {
        var created = protocol switch
        {
            DeviceProtocol.LumiBle => ProtocolDeviceRegistry.TryCreateBle(pdid, "device", NullLoggerFactory.Instance, out var device) ? device : null,
            DeviceProtocol.LumiMesh => ProtocolDeviceRegistry.TryCreateMesh(pdid, "device", NullLoggerFactory.Instance, out var device) ? device : null,
            _ => ProtocolDeviceRegistry.TryCreate(protocol, model, "device", 1, NullLoggerFactory.Instance, out var device) ? device : null,
        };
        created.Should().NotBeNull($"{protocol} model '{model}' is registered");
        return (ProtocolMappedDeviceBase)created;
    }

    private static void ParseWithoutException(ProtocolMappedDeviceBase device, DeviceProtocol protocol, string key, string value, string context)
    {
        try { device.ParseData(BuildPayload(device, protocol, key, value)); }
        catch (Exception exception)
        {
            Assert.Fail($"{context}: {exception}");
        }
    }

    // The payload a gateway passes to the device for one value, in the wire format of the protocol
    private static string BuildPayload(ProtocolMappedDeviceBase device, DeviceProtocol protocol, string key, string value)
    {
        if (protocol == DeviceProtocol.XiaomiGateway2)
            return $"{{{JsonSerializer.Serialize(key)}:{value}}}";

        var parts = key.Split(':');
        if (parts.Length == 3 && parts[0] == "mi")
            return $"[{{\"siid\":{parts[1]},\"piid\":{parts[2]},\"value\":{value}}}]";
        if (parts.Length == 3 && parts[0] == "event")
        {
            var eiid = parts[2] == "*" ? "1" : parts[2];
            return $"[{{\"siid\":{parts[1]},\"eiid\":{eiid},\"arguments\":{value}}}]";
        }
        if (protocol == DeviceProtocol.LumiBle && !device.UsesMiSpecBle)
            return $"{{\"evt\":[{{\"eid\":{key},\"edata\":{value}}}]}}";

        return $"[{{\"res_name\":{JsonSerializer.Serialize(key)},\"value\":{value}}}]";
    }

    private static string RandomHex(Random random)
    {
        var bytes = new byte[random.Next(1, 9)];
        random.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [Fact]
    public void Sqlite_reader_rejects_mutated_databases_quickly_with_documented_format_errors()
    {
        var database = CreateDatabase();
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var mutated = (byte[])database.Clone();
            var offset = random.Next(16, mutated.Length);
            mutated[offset] ^= (byte)(1 << random.Next(8));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try { SqliteTableReader.TryReadTable(mutated, "sample", ["value"], out _); }
            catch (InvalidDataException) { }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}: {exception}");
            }
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"seed {SeededMutator.Seed}, iteration {iteration}: SQLite parser took {stopwatch.Elapsed}.");
        }
    }

    [Fact]
    public void Hotspot_and_ble_parsers_only_throw_documented_input_errors()
    {
        var random = new Random(SeededMutator.Seed);
        var iterations = SeededMutator.Iterations;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var input = SeededMutator.RandomText(random);
            try { HotspotClientsParser.Parse(input); }
            catch (FormatException) { }
            catch (ArgumentException) { }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}, hotspot: {exception}");
            }

            try
            {
                BleValueDecoder.DecodeByte(input, "a battery value");
                BleValueDecoder.DecodeSignedTenths(input);
                BleValueDecoder.DecodeUnsignedLittleEndian(input, random.Next(1, 5));
            }
            catch (FormatException) { }
            catch (ArgumentException) { }
            catch (Exception exception)
            {
                Assert.Fail($"seed {SeededMutator.Seed}, iteration {iteration}, BLE: {exception}");
            }
        }
    }

    private static List<string> LoadFixtureMessages(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recorded", fileName);
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.GetProperty("message").GetString())
            .ToList();
    }

    private static byte[] CreateDatabase()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE sample(value INTEGER);";
                command.ExecuteNonQuery();
                for (var index = 0; index < 50; index++)
                {
                    command.CommandText = "INSERT INTO sample VALUES($value);";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$value", index);
                    command.ExecuteNonQuery();
                }
            }
            return File.ReadAllBytes(path);
        }
        finally { File.Delete(path); }
    }
}
