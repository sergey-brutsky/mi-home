using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using MiHomeLib.MqttGateway;

namespace MiHomeBenchmarks;

[MemoryDiagnoser]
public class SqliteReaderBenchmarks
{
    private string _databasePath;
    private byte[] _database;

    [GlobalSetup]
    public void Setup()
    {
        _databasePath = Path.GetTempFileName();
        using (var connection = new SqliteConnection($"Data Source={_databasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE sample(value INTEGER, name TEXT);";
            command.ExecuteNonQuery();
            for (var index = 0; index < 50; index++)
            {
                command.CommandText = "INSERT INTO sample VALUES($value, $name);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$value", index);
                command.Parameters.AddWithValue("$name", $"device-{index}");
                command.ExecuteNonQuery();
            }
        }
        _database = File.ReadAllBytes(_databasePath);
    }

    [Benchmark]
    public bool ReadTable() => SqliteTableReader.TryReadTable(_database, "sample", ["value", "name"], out _);

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }
}
