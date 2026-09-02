using System.Text;
using System.Text.RegularExpressions;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The bytes-only reader over authored state directories.
/// </summary>
/// <remarks>
/// Every database here is built by <see cref="SyntheticStateDatabase" />, byte
/// by byte. No manager's file is read by any check in this class.
/// </remarks>
public sealed partial class StateDatabaseTests
{
    private const string Wanted = "persistent###mods###";
    private const string Credential = "confidential###account###apiKey";
    private const int BlockSize = 32768;

    private static readonly string[] Prefixes = [Wanted];

    [Fact]
    public void ADirectoryWithNoPointerHoldsNoDatabase()
    {
        using var scratch = new SyntheticStateDatabase { WritePointer = false };
        scratch.Table(("persistent###mods###a", "1"));

        Assert.Null(StateDatabase.In(scratch.Write(), Prefixes));
    }

    [Fact]
    public void ADirectoryThatIsNotThereHoldsNoDatabase() =>
        Assert.Null(StateDatabase.In(
            Path.Combine(Path.GetTempPath(), "ripperdoc-absent-" + Guid.NewGuid().ToString("N")),
            Prefixes));

    [Fact]
    public void EveryKeyIsCountedAndTheModelledOnesAreRead()
    {
        using var scratch = new SyntheticStateDatabase();
        scratch.Table(("persistent###mods###a", "1"), ("persistent###mods###b", "2"));
        scratch.Log((Credential, "a secret"));

        var state = StateDatabase.In(scratch.Write(), Prefixes)!;

        Assert.Equal(3, state.KeysSeen);
        Assert.Equal(3, state.KeysLive);
        Assert.Equal(2, state.Values.Count);
        Assert.Equal("1", state.Text("persistent###mods###a"));
    }

    /// <summary>
    /// The credential-shaped key is seen and its value is never materialised.
    /// </summary>
    /// <remarks>
    /// The count is asserted as well as the absence, because a check that only
    /// asserted the absence would pass against a reader that never saw the key
    /// at all - and then say nothing about whether values are filtered.
    /// </remarks>
    [Fact]
    public void AValueOutsideTheModelledPrefixesIsNeverRead()
    {
        using var scratch = new SyntheticStateDatabase();
        scratch.Table(("persistent###mods###a", "1"), (Credential, "a secret"));

        var state = StateDatabase.In(scratch.Write(), Prefixes)!;

        Assert.Equal(2, state.KeysSeen);
        Assert.Equal(2, state.KeysLive);
        Assert.Null(state.Text(Credential));
        Assert.DoesNotContain(state.Values, pair => pair.Key.StartsWith("confidential"));
        Assert.Contains(state.Values, pair => pair.Key == "persistent###mods###a");
    }

    [Fact]
    public void TheNewestSequenceWinsAcrossTablesAndTheLog()
    {
        using var scratch = new SyntheticStateDatabase();
        scratch.Table(("persistent###mods###a", "old"));
        scratch.Table(("persistent###mods###a", "newer"));
        scratch.Log(("persistent###mods###a", "newest"));

        var state = StateDatabase.In(scratch.Write(), Prefixes)!;

        Assert.Equal("newest", state.Text("persistent###mods###a"));
        Assert.Equal(1, state.KeysSeen);
        Assert.Equal(3, state.EntriesRead);
    }

    [Fact]
    public void ADeletedKeyIsAbsentRatherThanHoldingWhatItHeld()
    {
        using var scratch = new SyntheticStateDatabase();
        scratch.Table(("persistent###mods###a", "1"), ("persistent###mods###b", "2"));
        scratch.Log(("persistent###mods###a", null));

        var state = StateDatabase.In(scratch.Write(), Prefixes)!;

        Assert.Null(state.Text("persistent###mods###a"));
        Assert.Equal(2, state.KeysSeen);
        Assert.Equal(1, state.KeysLive);
    }

    /// <summary>
    /// A record too long for one block is read whole from its fragments.
    /// </summary>
    [Fact]
    public void ARecordSplitAcrossBlocksIsReadWhole()
    {
        var long_ = new string('v', 70_000);
        using var scratch = new SyntheticStateDatabase();
        scratch.Log(("persistent###mods###a", long_));

        Assert.Equal(long_, StateDatabase.In(scratch.Write(), Prefixes)!.Text("persistent###mods###a"));
    }

    /// <summary>
    /// A table whose blocks are stored uncompressed reads the same as one whose
    /// blocks are compressed.
    /// </summary>
    [Fact]
    public void BothModelledCompressionsRead()
    {
        using var plain = new SyntheticStateDatabase { Compression = 0 };
        using var packed = new SyntheticStateDatabase { Compression = 1 };

        foreach (var scratch in new[] { plain, packed })
        {
            scratch.Table([.. Enumerable.Range(0, 40).Select(i => ($"persistent###mods###{i:D3}", $"{i}"))]);

            var state = StateDatabase.In(scratch.Write(), Prefixes)!;

            Assert.Equal(40, state.Values.Count);
            Assert.Equal("17", state.Text("persistent###mods###017"));
        }
    }

    public static TheoryData<string, string> Unmodelled => new()
    {
        { "comparator", "leveldb.SomeOtherComparator" },
        { "version-edit tag", "tagged 42" },
        { "log record type", "record of type 9" },
        { "entry kind", "entry of kind 7" },
        { "block checksum", "checksum does not match" },
        { "log checksum", "checksum does not match" },
        { "table magic", "does not end with the eight bytes" },
        { "declared table missing", "holds part of the state and there is no such file" },
        { "pointer names no manifest", "there is no such file" },
    };

    /// <summary>
    /// Everything outside the modelled subset is refused by name.
    /// </summary>
    /// <remarks>
    /// Each row's neighbour is the check below: the same database, built
    /// without that one change, reads green. A refusal that fired on any
    /// database would be a reader that refuses everything.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Unmodelled))]
    public void WhatIsNotModelledIsRefusedByName(string marker, string saying)
    {
        using var scratch = Populated();
        Spoil(scratch, marker);

        var directory = scratch.Write();
        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(directory, Prefixes));

        Assert.Contains(saying, refusal.Message, StringComparison.Ordinal);

        // A refusal that named nothing on disk would leave a reader with a
        // sentence and no place to look.
        Assert.Contains(
            Directory.GetFiles(directory).Select(Path.GetFileName).OfType<string>(),
            name => refusal.Message.Contains($"'{name}'", StringComparison.Ordinal));
    }

    /// <summary>
    /// A block compressed a way this reader does not model is refused by name.
    /// </summary>
    /// <remarks>
    /// Its own check rather than a row of the theory above, so that a sabotage
    /// of the modelled set can name the one check it must red.
    /// </remarks>
    [Fact]
    public void ABlockCompressedAWayThisReaderDoesNotModelIsRefusedByName()
    {
        using var scratch = Populated();
        scratch.FirstBlockCompression = 3;

        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(scratch.Write(), Prefixes));

        Assert.Contains("compressed by method 3", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A pointer naming something that is not a file name in the directory is
    /// refused where the platform says so.
    /// </summary>
    /// <remarks>
    /// A drive-relative name is a file name on Linux and is not one on Windows,
    /// so the outcome asserted here is the platform's own, not a constant. The
    /// neighbour is the plain-name check below, which reads green on both.
    /// </remarks>
    [Fact]
    public void APointerNamingSomethingOtherThanAFileNameInThatDirectoryIsRefused()
    {
        using var scratch = Populated();
        scratch.PointerText = "C:MANIFEST-000005";

        var directory = scratch.Write();
        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(directory, Prefixes));

        Assert.Contains(
            OperatingSystem.IsWindows()
                ? "this reader models it as holding one file name in that same directory"
                : $"there is no such file in '{directory}'",
            refusal.Message,
            StringComparison.Ordinal);
    }

    private static void Spoil(SyntheticStateDatabase scratch, string marker)
    {
        switch (marker)
        {
            case "comparator": scratch.Comparator = "leveldb.SomeOtherComparator"; break;
            case "version-edit tag": scratch.ExtraVersionEditTag = 42; break;
            case "log record type": scratch.FirstLogRecordType = 9; break;
            case "entry kind": scratch.FirstLogEntryKind = 7; break;
            case "block checksum": scratch.BreakFirstBlockChecksum = true; break;
            case "log checksum": scratch.BreakFirstLogChecksum = true; break;
            case "table magic": scratch.BreakTableMagic = true; break;
            case "declared table missing": scratch.DeclareAMissingTable = true; break;
            default: scratch.PointerText = "MANIFEST-000999"; break;
        }
    }

    /// <summary>
    /// The same database, unspoiled, reads green - so each refusal above is
    /// about the one change its row makes.
    /// </summary>
    [Fact]
    public void TheNeighbourEveryRefusalIsMeasuredAgainstReadsGreen()
    {
        using var scratch = Populated();

        var state = StateDatabase.In(scratch.Write(), Prefixes)!;

        Assert.Equal(4, state.Values.Count);
        Assert.Equal("1", state.Text("persistent###mods###a"));
    }

    /// <remarks>
    /// Bounded rather than asserted directly: the defect this is written
    /// against does not return at all, and a check that called straight into it
    /// would hang the suite instead of failing it.
    /// </remarks>
    [Fact]
    public async Task ABlockBeginningWithPaddingIsPassedOverRatherThanReadForever()
    {
        var read = Task.Run(() => LogRecords.In(new byte[BlockSize * 2], "000001.log"));
        var first = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.True(
            ReferenceEquals(first, read),
            "a file of whole padded blocks did not finish reading");
        Assert.Empty(await read);
    }

    /// <summary>
    /// A table whose two block bounds are each a legal length but overrun added
    /// together is refused by name, not indexed past.
    /// </summary>
    [Fact]
    public void ABlockBoundThatOverrunsOnlyWhenAddedIsRefusedByName()
    {
        var footer = new byte[TableFile.FooterSize];
        var at = 0;

        Varint(footer, ref at, 0);
        Varint(footer, ref at, 0);
        Varint(footer, ref at, 2_000_000_000);
        Varint(footer, ref at, 2_000_000_000);
        BitConverter.GetBytes(TableFile.Magic).CopyTo(footer, TableFile.FooterSize - 8);

        var refusal = Assert.Throws<StateReadException>(
            () => TableFile.ReadInto(footer, "000001.ldb", Nothing));

        Assert.Contains(
            "names a block of 2000000000 bytes at byte 2000000000, which is not within the 48 "
            + "bytes there are",
            refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A compressed literal declaring more bytes than a length can hold is
    /// refused by name rather than turned into a negative one.
    /// </summary>
    [Fact]
    public void ALiteralRunLargerThanALengthCanHoldIsRefusedByName()
    {
        var refusal = Assert.Throws<StateReadException>(
            () => Snappy.Decompress(
                [0x08, 0xFC, 0x00, 0x00, 0x00, 0x80, 1, 2, 3, 4], "a block"));

        Assert.Contains(
            "larger than anything this reader can hold",
            refusal.Message,
            StringComparison.Ordinal);
    }

    public static TheoryData<string, string> DeclaredLengths => new()
    {
        { "batch key length", "names a key of 2147483646 bytes" },
        { "version edit length", "names a value of 2147483646 bytes" },
        { "decompressed length", "declares 2147483647 decompressed bytes" },
    };

    /// <summary>
    /// A declared length the bytes cannot hold is refused by name at every
    /// site that reads one, including a length that wraps the sum in an int.
    /// </summary>
    /// <remarks>
    /// Each row's neighbour is the same database with the declaration left
    /// true, which is the whole of <see cref="Populated" /> as the checks
    /// around it read it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(DeclaredLengths))]
    public void ADeclaredLengthTheBytesCannotHoldIsRefusedByName(string marker, string saying)
    {
        using var scratch = Populated();

        switch (marker)
        {
            case "batch key length": scratch.DeclaredKeyLengthOfFirstLogEntry = int.MaxValue - 1; break;
            case "version edit length": scratch.DeclaredComparatorLength = int.MaxValue - 1; break;
            default: scratch.DeclaredDecompressedLengthOfFirstBlock = int.MaxValue; break;
        }

        var directory = scratch.Write();
        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(directory, Prefixes));

        Assert.Contains(saying, refusal.Message, StringComparison.Ordinal);
        Assert.Contains(
            Directory.GetFiles(directory).Select(Path.GetFileName).OfType<string>(),
            name => refusal.Message.Contains($"'{name}'", StringComparison.Ordinal));
    }

    /// <summary>
    /// A refusal from inside a block's compressed bytes places its own figure
    /// within them, so the two figures it carries do not read as one
    /// coordinate.
    /// </summary>
    [Fact]
    public void ARefusalFromInsideABlockPlacesItsFigureWithinTheCompressedBytes()
    {
        using var scratch = Populated();
        // A preamble of 16, then a literal tag declaring 9 bytes with 1 there.
        scratch.CompressedBodyOfFirstBlock = [0x10, 0x20, 0x61];

        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(scratch.Write(), Prefixes));

        Assert.Contains(
            "the compressed form of the block at byte 0 of '000001.ldb' names a literal run of 9 "
            + "bytes at byte 2, which is not within the 3 bytes there are",
            refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A block at the highest ratio the fixture's own compressor reaches
    /// reads through the whole reader.
    /// </summary>
    [Fact]
    public void ABlockAtTheHighestRatioTheFixturesCompressorReachesReads()
    {
        using var scratch = new SyntheticStateDatabase();
        var long_ = new string('v', 70_000);
        scratch.Table(("persistent###mods###a", long_));

        Assert.Equal(long_, StateDatabase.In(scratch.Write(), Prefixes)!.Text("persistent###mods###a"));
    }

    /// <summary>
    /// A block producing more than its compressed bytes divided by three
    /// reads, and the same block declaring one more is refused for what it
    /// produced - so the ceiling is the format's, and a block under it is not
    /// refused by it.
    /// </summary>
    [Fact]
    public void ABlockUnderItsCeilingReadsAndOneDeclaringMoreIsRefusedForWhatItProduced()
    {
        // A one-byte literal, then a 64-byte copy from one back: 65 bytes out
        // of five compressed, which a floor of 64 * (5 / 3) would refuse.
        byte[] body = [0x00, 0x76, 0xFE, 0x01, 0x00];

        Assert.Equal(
            new string('v', 65),
            Encoding.UTF8.GetString(Snappy.Decompress([65, .. body], "a block")));

        var refusal = Assert.Throws<StateReadException>(
            () => Snappy.Decompress([66, .. body], "a block"));

        Assert.Contains(
            "a block declares 66 decompressed bytes and produced 65",
            refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A preamble the ceiling admits and no array can hold is refused by
    /// name, and the refusal says which of the two bounds refused it.
    /// </summary>
    /// <remarks>
    /// The ceiling reaches the array maximum only over a block of a hundred
    /// megabytes, which is what this check allocates. Its neighbour is the
    /// same preamble over a block small enough for the ceiling to refuse.
    /// </remarks>
    [Fact]
    public void APreambleTheCeilingAdmitsAndNoArrayCanHoldIsRefusedByName()
    {
        byte[] preamble = [0xFF, 0xFF, 0xFF, 0xFF, 0x07];
        var wide = new byte[100_663_300];
        preamble.CopyTo(wide, 0);

        var byTheArray = Assert.Throws<StateReadException>(
            () => Snappy.Decompress(wide, "a block"));
        var byTheCeiling = Assert.Throws<StateReadException>(
            () => Snappy.Decompress([.. preamble, 0, 0, 0], "a block"));

        Assert.Contains(
            "a block declares 2147483647 decompressed bytes, and this reader can hold at most "
            + $"{Array.MaxLength} in one block",
            byTheArray.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "a block declares 2147483647 decompressed bytes, and its 3 compressed bytes can "
            + "produce at most 64",
            byTheCeiling.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A block declaring one byte over its ceiling is refused by the ceiling,
    /// and the figures the refusal carries are the ceiling's own.
    /// </summary>
    [Fact]
    public void ABlockDeclaringOneOverItsCeilingIsRefusedNamingTheCeiling()
    {
        using var scratch = Populated();
        scratch.DeclareOneOverTheCeilingOnFirstBlock = true;

        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(scratch.Write(), Prefixes));

        var figures = CeilingRefusal().Match(refusal.Message);

        Assert.True(figures.Success, refusal.Message);

        var declared = long.Parse(figures.Groups["declared"].Value);
        var compressed = long.Parse(figures.Groups["compressed"].Value);
        var ceiling = long.Parse(figures.Groups["ceiling"].Value);

        Assert.Equal(64 * ((compressed + 2) / 3), ceiling);
        Assert.Equal(ceiling + 1, declared);
    }

    /// <summary>
    /// The files named are the ones the manifest said hold state, and the two
    /// that were read to find them are not among them.
    /// </summary>
    [Fact]
    public void TheFilesNamedAreTheOnesTheManifestSaidHoldState()
    {
        using var scratch = new SyntheticStateDatabase();
        scratch.Table(("persistent###mods###a", "1"));
        scratch.Table(("persistent###mods###b", "2"));
        scratch.Log(("persistent###mods###c", "3"));

        var directory = scratch.Write();
        var state = StateDatabase.In(directory, Prefixes)!;

        Assert.Equal(3, state.FilesRead.Count);
        Assert.Equal(2, state.FilesRead.Count(name => name.EndsWith(".ldb", StringComparison.Ordinal)));
        Assert.Single(state.FilesRead, name => name.EndsWith(".log", StringComparison.Ordinal));
        Assert.DoesNotContain(StateVersion.PointerName, state.FilesRead);
        Assert.DoesNotContain(
            state.FilesRead,
            name => name.StartsWith("MANIFEST", StringComparison.Ordinal));
    }

    public static TheoryData<string, byte[]> CopyTags => new()
    {
        // literal "abcd", then a four-byte copy from four back, spelled three
        // ways: a one-byte offset, a two-byte offset, and a four-byte one.
        { "copy tag 1", [0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x01, 0x04] },
        { "copy tag 2", [0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x0E, 0x04, 0x00] },
        { "copy tag 3", [0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x0F, 0x04, 0x00, 0x00, 0x00] },
    };

    /// <summary>
    /// All three copy tags decode, which is what the modelled subset claims of
    /// them.
    /// </summary>
    /// <remarks>
    /// Authored byte by byte because the fixture's own encoder emits only the
    /// two-byte form, so the other two arms have no fixture that reaches them.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CopyTags))]
    public void EveryModelledCopyTagDecodes(string tag, byte[] block)
    {
        Assert.Equal("abcdabcd", Encoding.UTF8.GetString(Snappy.Decompress(block, tag)));
    }

    public static TheoryData<string, byte[], string> TagsCutShort => new()
    {
        // literal "abcd", then a tag whose trailing bytes are not there: the
        // two copy tags that carry an offset, and a literal whose length is
        // carried after the tag.
        { "copy tag 2", [0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x0E, 0x04], "a copy offset of 2 bytes at byte 7, which is not within the 8 bytes there are" },
        { "copy tag 3", [0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x0F, 0x04, 0x00], "a copy offset of 4 bytes at byte 7, which is not within the 9 bytes there are" },
        { "literal length", [0x08, 0xF4, 0x00], "a literal run's length of 2 bytes at byte 2, which is not within the 3 bytes there are" },
    };

    /// <summary>
    /// A tag cut short is refused by the name of what its missing bytes were.
    /// </summary>
    [Theory]
    [MemberData(nameof(TagsCutShort))]
    public void ATagCutShortIsRefusedByTheNameOfWhatItWasReading(string tag, byte[] block, string saying)
    {
        var refusal = Assert.Throws<StateReadException>(() => Snappy.Decompress(block, tag));

        Assert.Contains($"{tag} names {saying}", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A write-ahead log numbered above the one the manifest names is refused:
    /// the state may have been left part-way through a flush.
    /// </summary>
    [Fact]
    public void ALogNumberedAboveTheOneTheManifestNamesIsRefusedByName()
    {
        using var scratch = Populated();
        var directory = scratch.Write();

        File.WriteAllBytes(Path.Combine(directory, $"{Newest(directory) + 1:D6}.log"), []);

        var refusal = Assert.Throws<StateReadException>(
            () => StateDatabase.In(directory, Prefixes));

        Assert.Contains("the manifest does not name", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("mid-flush", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A leftover log numbered below the one the manifest names is left unread,
    /// which is what makes the refusal above about the flush and not about the
    /// directory holding an extra file.
    /// </summary>
    /// <remarks>
    /// Its bytes are not a log. Reading it would refuse on the framing, so a
    /// green here is a reading that never opened it.
    /// </remarks>
    [Fact]
    public void ALogNumberedBelowTheOneTheManifestNamesIsLeftUnread()
    {
        using var scratch = Populated();
        var directory = scratch.Write();
        var leftover = $"{Newest(directory) - 1:D6}.log";

        File.WriteAllBytes(Path.Combine(directory, leftover), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        var state = StateDatabase.In(directory, Prefixes)!;

        Assert.Equal(4, state.Values.Count);
        Assert.DoesNotContain(leftover, state.FilesRead);
    }

    private static ulong Newest(string directory) =>
        Directory.EnumerateFiles(directory, "*.log")
            .Select(path => ulong.Parse(Path.GetFileNameWithoutExtension(path)))
            .Max();

    private static void Nothing(
        ReadOnlySpan<byte> key, ulong sequence, bool isValue, ReadOnlySpan<byte> value)
    {
    }

    [GeneratedRegex(
        @"declares (?<declared>\d+) decompressed bytes, and its (?<compressed>\d+) compressed "
        + @"bytes can produce at most (?<ceiling>\d+)\.")]
    private static partial Regex CeilingRefusal();

    private static void Varint(byte[] into, ref int at, ulong value)
    {
        while (value >= 0x80)
        {
            into[at++] = (byte)(value | 0x80);
            value >>= 7;
        }

        into[at++] = (byte)value;
    }

    private static SyntheticStateDatabase Populated()
    {
        var scratch = new SyntheticStateDatabase();

        scratch.Table(("persistent###mods###a", "1"), ("persistent###mods###b", "2"));
        scratch.Log(("persistent###mods###c", "3"), ("persistent###mods###d", "4"));

        return scratch;
    }
}
