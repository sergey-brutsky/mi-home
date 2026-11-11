using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MiHomeLib.MqttGateway;

internal static class SqliteTableReader
{
    private static readonly byte[] FileHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");

    public static bool TryReadTable(byte[] database, string tableName, string[] columns, out List<object[]> rows)
    {
        var reader = new DatabaseReader(database);
        return reader.TryReadTable(tableName, columns, out rows);
    }

    private sealed class DatabaseReader
    {
        private readonly byte[] _database;
        private readonly int _pageSize;
        private readonly int _usablePageSize;
        private readonly int _encoding;

        public DatabaseReader(byte[] database)
        {
            if (database is null || database.Length < 100)
                throw new InvalidDataException("The SQLite database header is missing or incomplete.");

            for (var i = 0; i < FileHeader.Length; i++)
            {
                if (database[i] != FileHeader[i])
                    throw new InvalidDataException("The downloaded file is not a SQLite database.");
            }

            _database = database;
            _pageSize = ReadUInt16(database, 16);
            if (_pageSize == 1)
                _pageSize = 65536;
            if (_pageSize < 512 || _pageSize > 65536 || (_pageSize & (_pageSize - 1)) != 0)
                throw new InvalidDataException("The SQLite database has an invalid page size.");

            var reservedBytes = database[20];
            _usablePageSize = _pageSize - reservedBytes;
            if (_usablePageSize < 480 || database.Length < _pageSize)
                throw new InvalidDataException("The SQLite database has an invalid usable page size.");

            var encoding = ReadUInt32(database, 56);
            if (encoding > 3)
                throw new InvalidDataException("The SQLite database uses an unsupported text encoding.");
            _encoding = (int)encoding;
            if (_encoding == 0)
                _encoding = 1;
            if (_encoding < 1 || _encoding > 3)
                throw new InvalidDataException("The SQLite database uses an unsupported text encoding.");
        }

        public bool TryReadTable(string tableName, string[] requestedColumns, out List<object[]> rows)
        {
            var table = FindTable(tableName);
            if (table is null)
            {
                rows = [];
                return false;
            }

            var columnOrdinals = ResolveColumnOrdinals(table.CreateSql, requestedColumns);
            rows = ReadBtree(table.RootPage, new HashSet<uint>())
                .ConvertAll(record =>
                {
                    var selected = new object[columnOrdinals.Length];
                    for (var i = 0; i < columnOrdinals.Length; i++)
                    {
                        if (columnOrdinals[i] >= record.Length)
                            throw new InvalidDataException($"A row in SQLite table '{tableName}' has too few columns.");
                        selected[i] = record[columnOrdinals[i]];
                    }
                    return selected;
                });
            return true;
        }

        private TableInfo FindTable(string tableName)
        {
            foreach (var row in ReadBtree(1, new HashSet<uint>()))
            {
                if (row.Length < 5 ||
                    !string.Equals(row[0] as string, "table", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(row[1] as string, tableName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (row[3] is not long rootPage || rootPage < 1 || rootPage > uint.MaxValue ||
                    row[4] is not string createSql)
                    throw new InvalidDataException($"SQLite table '{tableName}' has invalid schema metadata.");

                return new TableInfo((uint)rootPage, createSql);
            }
            return null;
        }

        private List<object[]> ReadBtree(uint pageNumber, HashSet<uint> visitedPages)
        {
            if (!visitedPages.Add(pageNumber))
                throw new InvalidDataException("The SQLite database contains a cyclic or shared table b-tree page.");

            var pageOffset = GetPageOffset(pageNumber);
            var headerOffset = pageOffset + (pageNumber == 1 ? 100 : 0);
            EnsureRange(headerOffset, 8);

            var pageType = _database[headerOffset];
            var cellCount = ReadUInt16(_database, headerOffset + 3);
            var pointerArray = headerOffset + (pageType == 0x05 ? 12 : 8);
            EnsureRange(pointerArray, checked(cellCount * 2));
            var rows = new List<object[]>();

            if (pageType == 0x0D)
            {
                for (var i = 0; i < cellCount; i++)
                {
                    var cellOffset = pageOffset + ReadUInt16(_database, pointerArray + i * 2);
                    rows.Add(ReadLeafCell(cellOffset));
                }
                return rows;
            }

            if (pageType != 0x05)
                throw new InvalidDataException($"SQLite table b-tree page {pageNumber} has unsupported type 0x{pageType:X2}.");

            for (var i = 0; i < cellCount; i++)
            {
                var cellOffset = pageOffset + ReadUInt16(_database, pointerArray + i * 2);
                EnsureRange(cellOffset, 4);
                var childPage = ReadUInt32(_database, cellOffset);
                rows.AddRange(ReadBtree(childPage, visitedPages));
            }

            var rightmostPage = ReadUInt32(_database, headerOffset + 8);
            rows.AddRange(ReadBtree(rightmostPage, visitedPages));
            return rows;
        }

        private object[] ReadLeafCell(int cellOffset)
        {
            var (payloadSizeValue, payloadSizeBytes) = ReadVarint(cellOffset);
            if (payloadSizeValue > int.MaxValue || payloadSizeValue > (ulong)_database.Length)
                throw new InvalidDataException("A SQLite table row is too large.");
            var payloadSize = (int)payloadSizeValue;
            var (_, rowIdBytes) = ReadVarint(cellOffset + payloadSizeBytes);
            var payloadOffset = cellOffset + payloadSizeBytes + rowIdBytes;

            var maxLocal = _usablePageSize - 35;
            var minLocal = ((_usablePageSize - 12) * 32 / 255) - 23;
            var localPayloadSize = payloadSize;
            if (payloadSize > maxLocal)
            {
                var overflowCandidate = minLocal + ((payloadSize - minLocal) % (_usablePageSize - 4));
                localPayloadSize = overflowCandidate <= maxLocal ? overflowCandidate : minLocal;
            }

            EnsureRange(payloadOffset, localPayloadSize);
            var payload = new byte[payloadSize];
            Buffer.BlockCopy(_database, payloadOffset, payload, 0, localPayloadSize);

            var bytesRemaining = payloadSize - localPayloadSize;
            if (bytesRemaining > 0)
            {
                var overflowPointerOffset = payloadOffset + localPayloadSize;
                EnsureRange(overflowPointerOffset, 4);
                var overflowPage = ReadUInt32(_database, overflowPointerOffset);
                var copied = localPayloadSize;
                var visitedOverflowPages = new HashSet<uint>();

                while (bytesRemaining > 0)
                {
                    if (overflowPage == 0 || !visitedOverflowPages.Add(overflowPage))
                        throw new InvalidDataException("The SQLite database has an invalid overflow-page chain.");

                    var overflowOffset = GetPageOffset(overflowPage);
                    EnsureRange(overflowOffset, 4);
                    var nextOverflowPage = ReadUInt32(_database, overflowOffset);
                    var pagePayloadSize = Math.Min(bytesRemaining, _usablePageSize - 4);
                    EnsureRange(overflowOffset + 4, pagePayloadSize);
                    Buffer.BlockCopy(_database, overflowOffset + 4, payload, copied, pagePayloadSize);
                    copied += pagePayloadSize;
                    bytesRemaining -= pagePayloadSize;
                    overflowPage = nextOverflowPage;
                }
            }

            return DecodeRecord(payload);
        }

        private object[] DecodeRecord(byte[] payload)
        {
            var (headerSizeValue, headerSizeBytes) = ReadVarint(payload, 0);
            if (headerSizeValue < (ulong)headerSizeBytes || headerSizeValue > (ulong)payload.Length)
                throw new InvalidDataException("The SQLite record has an invalid header size.");
            var headerSize = (int)headerSizeValue;

            var serialTypes = new List<ulong>();
            var headerPosition = headerSizeBytes;
            while (headerPosition < headerSize)
            {
                var (serialType, bytesRead) = ReadVarint(payload, headerPosition);
                serialTypes.Add(serialType);
                headerPosition += bytesRead;
            }
            if (headerPosition != headerSize)
                throw new InvalidDataException("The SQLite record header is malformed.");

            var record = new object[serialTypes.Count];
            var dataOffset = headerSize;
            for (var i = 0; i < serialTypes.Count; i++)
            {
                var serialType = serialTypes[i];
                var valueSize = GetSerialTypeSize(serialType);
                EnsureRange(payload, dataOffset, valueSize);
                record[i] = DecodeValue(payload, dataOffset, serialType, valueSize);
                dataOffset += valueSize;
            }
            if (dataOffset != payload.Length)
                throw new InvalidDataException("The SQLite record contains unexpected trailing data.");

            return record;
        }

        private object DecodeValue(byte[] bytes, int offset, ulong serialType, int size)
        {
            switch (serialType)
            {
                case 0:
                case 10:
                case 11:
                    return null;
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                    return ReadSignedInteger(bytes, offset, size);
                case 7:
                    var bits = ReadUInt64(bytes, offset);
                    return BitConverter.Int64BitsToDouble(unchecked((long)bits));
                case 8:
                    return 0L;
                case 9:
                    return 1L;
                default:
                    if (serialType >= 12 && (serialType & 1) == 0)
                    {
                        var blob = new byte[size];
                        Buffer.BlockCopy(bytes, offset, blob, 0, size);
                        return blob;
                    }
                    if (serialType >= 13 && (serialType & 1) == 1)
                        return DecodeText(bytes, offset, size);
                    throw new InvalidDataException($"SQLite record has reserved serial type {serialType}.");
            }
        }

        private string DecodeText(byte[] bytes, int offset, int size)
        {
            return _encoding switch
            {
                1 => Encoding.UTF8.GetString(bytes, offset, size),
                2 => Encoding.Unicode.GetString(bytes, offset, size),
                3 => Encoding.BigEndianUnicode.GetString(bytes, offset, size),
                _ => throw new InvalidDataException("The SQLite database uses an unsupported text encoding.")
            };
        }

        private static int[] ResolveColumnOrdinals(string createSql, string[] requestedColumns)
        {
            var definitions = GetColumnDefinitions(createSql);
            var ordinals = new int[requestedColumns.Length];
            for (var requested = 0; requested < requestedColumns.Length; requested++)
            {
                ordinals[requested] = definitions.FindIndex(
                    column => string.Equals(column, requestedColumns[requested], StringComparison.OrdinalIgnoreCase));
                if (ordinals[requested] < 0)
                    throw new InvalidDataException($"SQLite table is missing the expected column '{requestedColumns[requested]}'.");
            }
            return ordinals;
        }

        private static List<string> GetColumnDefinitions(string createSql)
        {
            var openParen = createSql.IndexOf('(');
            if (openParen < 0)
                throw new InvalidDataException("The SQLite table schema has no column list.");

            var definitions = new List<string>();
            var start = openParen + 1;
            var depth = 0;
            char quote = '\0';
            for (var i = start; i < createSql.Length; i++)
            {
                var current = createSql[i];
                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        if (i + 1 < createSql.Length && createSql[i + 1] == quote)
                            i++;
                        else
                            quote = '\0';
                    }
                    else if (quote == ']' && current == ']')
                        quote = '\0';
                    continue;
                }

                if (current == '"' || current == '\'' || current == '`' || current == '[')
                {
                    quote = current == '[' ? ']' : current;
                    continue;
                }
                if (current == '(')
                    depth++;
                else if (current == ')')
                {
                    if (depth == 0)
                    {
                        AddColumnDefinition(createSql, start, i - start, definitions);
                        return definitions;
                    }
                    depth--;
                }
                else if (current == ',' && depth == 0)
                {
                    AddColumnDefinition(createSql, start, i - start, definitions);
                    start = i + 1;
                }
            }

            throw new InvalidDataException("The SQLite table schema has an unterminated column list.");
        }

        private static void AddColumnDefinition(string sql, int start, int length, List<string> definitions)
        {
            var definition = sql.Substring(start, length).Trim();
            if (definition.Length == 0)
                throw new InvalidDataException("The SQLite table schema contains an empty column definition.");

            var first = definition[0];
            if (first == '"' || first == '\'' || first == '`' || first == '[')
            {
                var closing = first == '[' ? ']' : first;
                var end = definition.IndexOf(closing, 1);
                if (end < 0)
                    throw new InvalidDataException("The SQLite table schema contains an invalid quoted column name.");
                definitions.Add(definition.Substring(1, end - 1));
                return;
            }

            var nameEnd = 0;
            while (nameEnd < definition.Length && !char.IsWhiteSpace(definition[nameEnd]) && definition[nameEnd] != '(')
                nameEnd++;
            var name = definition.Substring(0, nameEnd);
            if (name.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("CHECK", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("FOREIGN", StringComparison.OrdinalIgnoreCase))
                return;
            definitions.Add(name);
        }

        private int GetPageOffset(uint pageNumber)
        {
            if (pageNumber == 0)
                throw new InvalidDataException("The SQLite database references page zero.");
            var offset = ((long)pageNumber - 1) * _pageSize;
            if (offset > int.MaxValue || offset + _pageSize > _database.Length)
                throw new InvalidDataException($"The SQLite database references missing page {pageNumber}.");
            return (int)offset;
        }

        private void EnsureRange(int offset, int length)
        {
            EnsureRange(_database, offset, length);
            if (length > _usablePageSize || offset % _pageSize + length > _pageSize)
                throw new InvalidDataException("A SQLite page structure extends beyond its page boundary.");
        }

        private static void EnsureRange(byte[] bytes, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > bytes.Length - length)
                throw new InvalidDataException("The SQLite database contains an out-of-range value.");
        }

        private (ulong Value, int BytesRead) ReadVarint(int offset)
        {
            return ReadVarint(_database, offset);
        }

        private static (ulong Value, int BytesRead) ReadVarint(byte[] bytes, int offset)
        {
            EnsureRange(bytes, offset, 1);
            ulong value = 0;
            for (var i = 0; i < 8; i++)
            {
                EnsureRange(bytes, offset + i, 1);
                var current = bytes[offset + i];
                value = (value << 7) | (uint)(current & 0x7F);
                if ((current & 0x80) == 0)
                    return (value, i + 1);
            }

            EnsureRange(bytes, offset + 8, 1);
            return ((value << 8) | bytes[offset + 8], 9);
        }

        private static int GetSerialTypeSize(ulong serialType)
        {
            if (serialType <= 4)
                return serialType switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, 4 => 4, _ => 0 };
            if (serialType == 5)
                return 6;
            if (serialType == 6 || serialType == 7)
                return 8;
            if (serialType == 8 || serialType == 9 || serialType == 10 || serialType == 11)
                return 0;
            if (serialType >= 12)
            {
                var size = serialType / 2 - (serialType % 2 == 0 ? 6UL : 6UL);
                if (size > int.MaxValue)
                    throw new InvalidDataException("A SQLite record value is too large.");
                return (int)size;
            }
            throw new InvalidDataException($"SQLite record has invalid serial type {serialType}.");
        }

        private static long ReadSignedInteger(byte[] bytes, int offset, int size)
        {
            long value = (bytes[offset] & 0x80) == 0 ? 0 : -1;
            for (var i = 0; i < size; i++)
                value = (value << 8) | bytes[offset + i];
            return value;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset) =>
            (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

        private static uint ReadUInt32(byte[] bytes, int offset) =>
            ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
            ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

        private static ulong ReadUInt64(byte[] bytes, int offset) =>
            ((ulong)ReadUInt32(bytes, offset) << 32) | ReadUInt32(bytes, offset + 4);

        private sealed class TableInfo(uint rootPage, string createSql)
        {
            public uint RootPage { get; } = rootPage;
            public string CreateSql { get; } = createSql;
        }
    }
}
