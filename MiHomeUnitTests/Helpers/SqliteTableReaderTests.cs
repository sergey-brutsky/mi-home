using System;
using System.IO;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class SqliteTableReaderTests
{
    private static byte[] Database(string encoding = "UTF-8")
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA encoding='{encoding}'; CREATE TABLE sample(\"a,b\" TEXT, n INTEGER, r REAL, b BLOB, empty TEXT, flag INTEGER, big INTEGER, neg INTEGER); INSERT INTO sample VALUES('Привет', -123456, 1.25, X'00ABFF', NULL, 1, 1099511627776, -9223372036854775807);";
                command.ExecuteNonQuery();
            }
            return File.ReadAllBytes(path);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Every_Integer_Storage_Width_Is_Read_With_Sign()
    {
        var path = Path.GetTempFileName();
        long[] numbers = [0, 1, 127, -128, 32767, -32768, 8388607, -8388608, 2147483647, -2147483648, 140737488355327, -140737488355328, long.MaxValue, long.MinValue];
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE numbers(value INTEGER);";
                command.ExecuteNonQuery();
                foreach (var value in numbers)
                {
                    command.CommandText = "INSERT INTO numbers VALUES($value)";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$value", value);
                    command.ExecuteNonQuery();
                }
            }
            SqliteTableReader.TryReadTable(File.ReadAllBytes(path), "numbers", ["value"], out var rows).Should().BeTrue();
            rows.ConvertAll(row => (long)row[0]).Should().Equal(numbers);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Cyclic_Table_Page_Is_Rejected()
    {
        var db = Database();
        var pageSize = (db[16] << 8) | db[17];
        var offset = pageSize; // sample is the first table after sqlite_schema
        db[offset] = 5; // interior table page with no cells and a rightmost child
        db[offset + 3] = 0;
        db[offset + 4] = 0;
        db[offset + 8] = 0;
        db[offset + 9] = 0;
        db[offset + 10] = 0;
        db[offset + 11] = 2; // points back to itself
        Action read = () => SqliteTableReader.TryReadTable(db, "sample", ["n"], out _);
        read.Should().Throw<InvalidDataException>().WithMessage("*cyclic*");
    }

    [Theory]
    [InlineData("UTF-8")]
    [InlineData("UTF-16le")]
    [InlineData("UTF-16be")]
    public void Reads_Text_Numeric_Blob_And_Null_In_Different_Encodings(string encoding)
    {
        SqliteTableReader.TryReadTable(Database(encoding), "sample", ["a,b", "n", "r", "b", "empty", "flag", "big", "neg"], out var rows).Should().BeTrue();
        rows.Should().ContainSingle();
        rows[0][0].Should().Be("Привет");
        rows[0][1].Should().Be(-123456L);
        rows[0][2].Should().Be(1.25d);
        ((byte[])rows[0][3]).Should().Equal(0, 171, 255);
        rows[0][4].Should().BeNull();
        rows[0][5].Should().Be(1L);
        rows[0][6].Should().Be(1099511627776L);
        rows[0][7].Should().Be(-9223372036854775807L);
    }

    [Fact]
    public void Missing_Table_Returns_False_But_Missing_Column_Throws()
    {
        var db = Database();
        SqliteTableReader.TryReadTable(db, "absent", ["x"], out var rows).Should().BeFalse();
        rows.Should().BeEmpty();
        Action read = () => SqliteTableReader.TryReadTable(db, "sample", ["absent"], out _);
        read.Should().Throw<InvalidDataException>().WithMessage("*column*");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(16, 3)]
    [InlineData(56, 255)]
    [InlineData(100, 255)]
    public void Corrupt_Header_Or_Page_Is_Rejected(int offset, byte value)
    {
        var db = Database();
        db[offset] = value;
        Action read = () => SqliteTableReader.TryReadTable(db, "sample", ["n"], out _);
        read.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(110)]
    public void Truncated_Database_Is_Rejected(int size)
    {
        var db = Database();
        Array.Resize(ref db, size);
        Action read = () => SqliteTableReader.TryReadTable(db, "sample", ["n"], out _);
        read.Should().Throw<InvalidDataException>();
    }
}
