using System.Text;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Builds a manager state directory byte by byte, so that every fixture the
/// reader is held against is authored here rather than taken from anyone's
/// install.
/// </summary>
/// <remarks>
/// Lives in the test assembly and nowhere else. The engine has no write path to
/// a manager's state and this is not one - it exists so the reader can be shown
/// a database whose every byte is known, including the bytes it must refuse.
/// </remarks>
internal sealed class SyntheticStateDatabase : IDisposable
{
    private const int BlockSize = 32768;
    private const uint MaskDelta = 0xA282EAD8;
    private const ulong Magic = 0xdb4775248b80fb57;

    private static readonly uint[] Crc = BuildTable();

    private readonly List<List<Entry>> tables = [];
    private readonly List<Entry> log = [];
    private ulong sequence = 1;

    /// <summary>What the manifest declares the key ordering to be.</summary>
    internal string Comparator { get; set; } = "leveldb.BytewiseComparator";

    /// <summary>A version-edit tag to write beside the modelled ones, or null.</summary>
    internal int? ExtraVersionEditTag { get; set; }

    /// <summary>How each table's blocks are compressed.</summary>
    internal byte Compression { get; set; } = 1;

    /// <summary>A compression byte to stamp on the first data block instead.</summary>
    internal byte? FirstBlockCompression { get; set; }

    /// <summary>Whether to leave the first data block's checksum wrong.</summary>
    internal bool BreakFirstBlockChecksum { get; set; }

    /// <summary>Whether to leave the log's first record checksum wrong.</summary>
    internal bool BreakFirstLogChecksum { get; set; }

    /// <summary>A record type to stamp on the log's first record instead.</summary>
    internal byte? FirstLogRecordType { get; set; }

    /// <summary>An entry kind to stamp on the log's first entry instead.</summary>
    internal byte? FirstLogEntryKind { get; set; }

    /// <summary>Whether to leave the first table's trailing magic wrong.</summary>
    internal bool BreakTableMagic { get; set; }

    /// <summary>Whether to declare a table the directory does not hold.</summary>
    internal bool DeclareAMissingTable { get; set; }

    /// <summary>Whether to write the pointer naming the manifest at all.</summary>
    internal bool WritePointer { get; set; } = true;

    /// <summary>What the pointer says, where it says something unusual.</summary>
    internal string? PointerText { get; set; }

    /// <summary>How many entries share a restart point in a table's blocks.</summary>
    internal int RestartInterval { get; set; } = 16;

    /// <summary>A key length to declare on the log's first entry instead of its own.</summary>
    internal ulong? DeclaredKeyLengthOfFirstLogEntry { get; set; }

    /// <summary>A length to declare on the manifest's comparator instead of its own.</summary>
    internal ulong? DeclaredComparatorLength { get; set; }

    /// <summary>
    /// A decompressed length to declare on the first data block's preamble
    /// instead of its own.
    /// </summary>
    internal ulong? DeclaredDecompressedLengthOfFirstBlock { get; set; }

    /// <summary>
    /// Bytes to write as the first data block's compressed body instead of
    /// its own, under a checksum that covers them.
    /// </summary>
    internal byte[]? CompressedBodyOfFirstBlock { get; set; }

    /// <summary>
    /// Adds a table file holding these keys, at sequence numbers below anything
    /// added after it.
    /// </summary>
    /// <param name="entries">The keys and their values; a null value deletes.</param>
    /// <returns>This builder.</returns>
    internal SyntheticStateDatabase Table(params (string Key, string? Value)[] entries)
    {
        tables.Add([.. entries.Select(entry => new Entry(entry.Key, entry.Value, sequence++))]);

        return this;
    }

    /// <summary>
    /// Adds entries to the write-ahead log, at sequence numbers above anything
    /// added before them.
    /// </summary>
    /// <param name="entries">The keys and their values; a null value deletes.</param>
    /// <returns>This builder.</returns>
    internal SyntheticStateDatabase Log(params (string Key, string? Value)[] entries)
    {
        log.AddRange(entries.Select(entry => new Entry(entry.Key, entry.Value, sequence++)));

        return this;
    }

    /// <summary>Where the database was written, once it has been.</summary>
    internal string? Root { get; private set; }

    /// <summary>
    /// Writes the database into a directory of its own.
    /// </summary>
    /// <returns>The directory.</returns>
    internal string Write() =>
        Write(Root = System.IO.Directory.CreateTempSubdirectory("ripperdoc-state-").FullName);

    /// <summary>Removes the directory this wrote, where it made one.</summary>
    public void Dispose()
    {
        if (Root is not null && System.IO.Directory.Exists(Root))
        {
            System.IO.Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// Writes the database into a directory.
    /// </summary>
    /// <param name="directory">Where to write it.</param>
    /// <returns>The directory.</returns>
    internal string Write(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);

        var numbers = new List<ulong>();

        for (var i = 0; i < tables.Count; i++)
        {
            var number = (ulong)i + 1;
            numbers.Add(number);
            File.WriteAllBytes(
                Path.Combine(directory, $"{number:D6}.ldb"), Table(tables[i], i == 0));
        }

        var logNumber = (ulong)tables.Count + 1;
        File.WriteAllBytes(Path.Combine(directory, $"{logNumber:D6}.log"), WriteLog());

        if (DeclareAMissingTable)
        {
            numbers.Add(logNumber + 99);
        }

        var manifest = $"MANIFEST-{logNumber + 1:D6}";
        File.WriteAllBytes(Path.Combine(directory, manifest), Manifest(numbers, logNumber));

        if (WritePointer)
        {
            File.WriteAllBytes(
                Path.Combine(directory, "CURRENT"),
                Encoding.UTF8.GetBytes((PointerText ?? manifest) + "\n"));
        }

        return directory;
    }

    private readonly record struct Entry(string Key, string? Value, ulong Sequence);

    private byte[] Manifest(List<ulong> tableNumbers, ulong logNumber)
    {
        var edit = new List<byte>();

        PutVarInt(edit, 1);
        PutLengthPrefixed(edit, Encoding.UTF8.GetBytes(Comparator), DeclaredComparatorLength);
        PutVarInt(edit, 2);
        PutVarInt(edit, logNumber);
        PutVarInt(edit, 3);
        PutVarInt(edit, logNumber + 2);
        PutVarInt(edit, 4);
        PutVarInt(edit, sequence);
        PutVarInt(edit, 9);
        PutVarInt(edit, 0);

        foreach (var number in tableNumbers)
        {
            PutVarInt(edit, 7);
            PutVarInt(edit, 0);
            PutVarInt(edit, number);
            PutVarInt(edit, 1024);
            PutTagged(edit, null, Encoding.UTF8.GetBytes("a"));
            PutTagged(edit, null, Encoding.UTF8.GetBytes("z"));
        }

        // Written last so that everything a reader needs is already in the edit
        // when it meets the tag it does not model.
        if (ExtraVersionEditTag is { } tag)
        {
            PutVarInt(edit, (ulong)tag);
            PutVarInt(edit, 0);
        }

        return Frame([[.. edit]], breakFirstChecksum: false, firstType: null);
    }

    private byte[] WriteLog()
    {
        if (log.Count == 0)
        {
            return Frame([], breakFirstChecksum: false, firstType: null);
        }

        var batch = new List<byte>();
        batch.AddRange(BitConverter.GetBytes(log[0].Sequence));
        batch.AddRange(BitConverter.GetBytes((uint)log.Count));

        for (var i = 0; i < log.Count; i++)
        {
            var entry = log[i];
            var kind = (byte)(entry.Value is null ? 0 : 1);

            batch.Add(i == 0 ? FirstLogEntryKind ?? kind : kind);
            PutLengthPrefixed(
                batch, Encoding.UTF8.GetBytes(entry.Key), i == 0 ? DeclaredKeyLengthOfFirstLogEntry : null);

            if (entry.Value is not null)
            {
                PutLengthPrefixed(batch, Encoding.UTF8.GetBytes(entry.Value));
            }
        }

        return Frame([[.. batch]], BreakFirstLogChecksum, FirstLogRecordType);
    }

    /// <remarks>
    /// A record longer than a block is split, which is the only way the
    /// fragmenting arms of the framing are exercised at all.
    /// </remarks>
    private static byte[] Frame(List<byte[]> records, bool breakFirstChecksum, byte? firstType)
    {
        var file = new List<byte>();
        var first = true;

        foreach (var record in records)
        {
            var at = 0;

            while (true)
            {
                var room = BlockSize - (file.Count % BlockSize);

                if (room < 7)
                {
                    file.AddRange(new byte[room]);
                    room = BlockSize;
                }

                var take = Math.Min(record.Length - at, room - 7);
                var start = at == 0;
                var end = at + take == record.Length;
                var type = (byte)(start ? end ? 1 : 2 : end ? 4 : 3);
                var payload = record.AsSpan(at, take);
                var stored = Mask(Checksum(type, payload));

                if (first && breakFirstChecksum)
                {
                    stored ^= 0xFFFFFFFF;
                }

                file.AddRange(BitConverter.GetBytes(stored));
                file.AddRange(BitConverter.GetBytes((ushort)take));
                file.Add(first && firstType is { } forced ? forced : type);
                file.AddRange(payload);
                first = false;
                at += take;

                if (end)
                {
                    break;
                }
            }
        }

        return [.. file];
    }

    private byte[] Table(List<Entry> entries, bool isFirst)
    {
        var sorted = entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
        var file = new List<byte>();
        var index = new List<(byte[] Key, int Offset, int Size)>();
        var block = new List<Entry>();

        void Flush()
        {
            if (block.Count == 0)
            {
                return;
            }

            var content = DataBlock(block);
            var offset = file.Count;
            var size = PutBlock(file, content, isFirst && index.Count == 0);

            index.Add((InternalKey(block[^1]), offset, size));
            block.Clear();
        }

        foreach (var entry in sorted)
        {
            block.Add(entry);

            if (block.Count == RestartInterval * 2)
            {
                Flush();
            }
        }

        Flush();

        var metaOffset = file.Count;
        var metaSize = PutBlock(file, Restarts([], []), corrupt: false);

        var indexEntries = new List<byte[]>();
        var indexValues = new List<byte[]>();

        foreach (var (key, offset, size) in index)
        {
            var handle = new List<byte>();
            PutVarInt(handle, (ulong)offset);
            PutVarInt(handle, (ulong)size);
            indexEntries.Add(key);
            indexValues.Add([.. handle]);
        }

        var indexOffset = file.Count;
        var indexSize = PutBlock(file, Restarts(indexEntries, indexValues), corrupt: false);

        var footer = new List<byte>();
        PutVarInt(footer, (ulong)metaOffset);
        PutVarInt(footer, (ulong)metaSize);
        PutVarInt(footer, (ulong)indexOffset);
        PutVarInt(footer, (ulong)indexSize);
        footer.AddRange(new byte[40 - footer.Count]);
        footer.AddRange(BitConverter.GetBytes(
            isFirst && BreakTableMagic ? ~Magic : Magic));

        file.AddRange(footer);

        return [.. file];
    }

    private byte[] DataBlock(List<Entry> entries) =>
        Restarts(
            [.. entries.Select(InternalKey)],
            [.. entries.Select(entry => Encoding.UTF8.GetBytes(entry.Value ?? string.Empty))]);

    private static byte[] InternalKey(Entry entry)
    {
        var key = Encoding.UTF8.GetBytes(entry.Key);
        var whole = new byte[key.Length + 8];

        key.CopyTo(whole, 0);
        BitConverter.GetBytes((entry.Sequence << 8) | (entry.Value is null ? 0u : 1u))
            .CopyTo(whole, key.Length);

        return whole;
    }

    /// <remarks>
    /// Real prefix compression with real restart points, because a builder that
    /// wrote every key whole would leave the reader's key-rebuilding arm
    /// unexercised by every fixture in the suite.
    /// </remarks>
    private byte[] Restarts(List<byte[]> keys, List<byte[]> values)
    {
        var content = new List<byte>();
        var points = new List<uint>();
        var previous = Array.Empty<byte>();

        for (var i = 0; i < keys.Count; i++)
        {
            var restart = i % Math.Max(RestartInterval, 1) == 0;

            if (restart)
            {
                points.Add((uint)content.Count);
                previous = [];
            }

            var shared = 0;

            while (shared < previous.Length
                && shared < keys[i].Length
                && previous[shared] == keys[i][shared])
            {
                shared++;
            }

            PutVarInt(content, (ulong)shared);
            PutVarInt(content, (ulong)(keys[i].Length - shared));
            PutVarInt(content, (ulong)values[i].Length);
            content.AddRange(keys[i].AsSpan(shared));
            content.AddRange(values[i]);
            previous = keys[i];
        }

        if (points.Count == 0)
        {
            points.Add(0);
        }

        foreach (var point in points)
        {
            content.AddRange(BitConverter.GetBytes(point));
        }

        content.AddRange(BitConverter.GetBytes((uint)points.Count));

        return [.. content];
    }

    private int PutBlock(List<byte> file, byte[] content, bool corrupt)
    {
        var compression = corrupt ? FirstBlockCompression ?? Compression : Compression;
        var body = compression == 1
            ? corrupt && CompressedBodyOfFirstBlock is { } forced
                ? forced
                : Compress(content, corrupt ? DeclaredDecompressedLengthOfFirstBlock : null)
            : content;

        if (compression is not (0 or 1))
        {
            body = content;
        }

        // A block's checksum covers its bytes and then the byte saying how they
        // are compressed, in that order; a record's covers its type byte first.
        var covered = new byte[body.Length + 1];
        body.CopyTo(covered, 0);
        covered[^1] = compression;

        var stored = Mask(Checksum(covered[0], covered.AsSpan(1)));

        if (corrupt && BreakFirstBlockChecksum)
        {
            stored ^= 0xFFFFFFFF;
        }

        file.AddRange(body);
        file.Add(compression);
        file.AddRange(BitConverter.GetBytes(stored));

        return body.Length;
    }

    private static byte[] Compress(byte[] data, ulong? declared = null)
    {
        var output = new List<byte>();
        PutVarInt(output, declared ?? (ulong)data.Length);

        var table = new Dictionary<int, int>();
        var literal = 0;
        var at = 0;

        while (at + 4 <= data.Length)
        {
            var hash = BitConverter.ToInt32(data, at);

            if (table.TryGetValue(hash, out var candidate)
                && at - candidate is > 0 and <= 65535
                && data.AsSpan(candidate, 4).SequenceEqual(data.AsSpan(at, 4)))
            {
                var length = 4;

                while (length < 64
                    && at + length < data.Length
                    && data[candidate + length] == data[at + length])
                {
                    length++;
                }

                PutLiteral(output, data, literal, at - literal);
                output.Add((byte)(((length - 1) << 2) | 2));
                output.AddRange(BitConverter.GetBytes((ushort)(at - candidate)));

                for (var i = at; i < at + length && i + 4 <= data.Length; i++)
                {
                    table[BitConverter.ToInt32(data, i)] = i;
                }

                at += length;
                literal = at;
                continue;
            }

            table[hash] = at;
            at++;
        }

        PutLiteral(output, data, literal, data.Length - literal);

        return [.. output];
    }

    private static void PutLiteral(List<byte> output, byte[] data, int at, int length)
    {
        while (length > 0)
        {
            var take = Math.Min(length, 60);

            output.Add((byte)((take - 1) << 2));
            output.AddRange(data.AsSpan(at, take));
            at += take;
            length -= take;
        }
    }

    private static void PutTagged(List<byte> into, int? tag, byte[] value)
    {
        if (tag is { } number)
        {
            PutVarInt(into, (ulong)number);
        }

        PutLengthPrefixed(into, value);
    }

    /// <remarks>
    /// The declared length is the one thing a fixture may lie about: the bytes
    /// written are always the value's own, so a reader that believes the
    /// declaration reads past them.
    /// </remarks>
    private static void PutLengthPrefixed(List<byte> into, byte[] value, ulong? declared = null)
    {
        PutVarInt(into, declared ?? (ulong)value.Length);
        into.AddRange(value);
    }

    private static void PutVarInt(List<byte> into, ulong value)
    {
        while (value >= 0x80)
        {
            into.Add((byte)(value | 0x80));
            value >>= 7;
        }

        into.Add((byte)value);
    }

    private static uint Mask(uint crc) => ((crc >> 15) | (crc << 17)) + MaskDelta;

    private static uint Checksum(byte first, ReadOnlySpan<byte> rest)
    {
        var crc = (0xFFFFFFFFu >> 8) ^ Crc[(0xFFFFFFFFu ^ first) & 0xFF];

        foreach (var b in rest)
        {
            crc = (crc >> 8) ^ Crc[(crc ^ b) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (var i = 0u; i < table.Length; i++)
        {
            var value = i;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ 0x82F63B78 : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
