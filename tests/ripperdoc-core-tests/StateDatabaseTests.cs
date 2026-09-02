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
public sealed class StateDatabaseTests
{
    private const string Wanted = "persistent###mods###";
    private const string Credential = "confidential###account###apiKey";

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

    private static SyntheticStateDatabase Populated()
    {
        var scratch = new SyntheticStateDatabase();

        scratch.Table(("persistent###mods###a", "1"), ("persistent###mods###b", "2"));
        scratch.Log(("persistent###mods###c", "3"), ("persistent###mods###d", "4"));

        return scratch;
    }
}
