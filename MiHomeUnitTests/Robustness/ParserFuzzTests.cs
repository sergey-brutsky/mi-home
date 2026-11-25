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
