using System.Text;
using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using WolvenKit.RED4.TweakDB;
using WolvenKit.RED4.Types;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The adapter that reads a real database, driven by databases built here.
/// </summary>
/// <remarks>
/// This is the one piece of the engine that talks to the shipped file, and the
/// shipped file is not this project's to redistribute - so its behaviour was
/// going unchecked on both sides: no tier (i) coverage, and the branches its
/// own documentation calls out never fire against real data either. A database
/// built in memory reaches them, with zero game-derived bytes involved.
/// </remarks>
public class TweakDatabaseSourceTests
{
    [Fact]
    public void RecordsComeBackWithTheirTypeNames()
    {
        var database = new TweakDB();
        database.Add("Test.thing", new gamedataItem_Record());

        var record = Assert.Single(Source(database).Records);

        Assert.Equal("gamedataItem_Record", record.TypeName);
        Assert.Equal(TweakIdentifier.Of("Test.thing"), record.Identifier);
    }

    [Fact]
    public void ARecordWhoseTypeTheDatabaseDoesNotGiveIsNamedRatherThanDropped()
    {
        // The claim this constant makes is that such a record "surfaces as a
        // type the schema does not cover instead of disappearing". Left
        // unchecked, a record could be dropped or silently attributed to some
        // other type and the count of records examined would still look right.
        var database = new TweakDB();
        database.Records.Add(TweakIdentifier.Of("Test.untyped"), null!);

        var record = Assert.Single(Source(database).Records);

        Assert.Equal(TweakDatabaseSource.UnknownRecordTypeName, record.TypeName);
    }

    [Fact]
    public void ARecordWithNoTypeIsCountedAndSurfacedByTheManifestToo()
    {
        var database = new TweakDB();
        database.Records.Add(TweakIdentifier.Of("Test.untyped"), null!);

        var manifest = ValidationManifest.Build(EmptySchema(), Source(database));

        Assert.Equal(1, manifest.RecordsExamined);
        Assert.Equal(
            new[] { TweakDatabaseSource.UnknownRecordTypeName },
            manifest.RecordTypesNotInSchema);
    }

    [Fact]
    public void AStoredValueIsFoundAndItsStorageTypeNamed()
    {
        var database = new TweakDB();
        database.Flats.Add("Test.thing.speed", new CFloat());

        Assert.True(
            Source(database).TryGetStoredValueType(TweakIdentifier.Of("Test.thing.speed"), out var storageType));
        Assert.Equal("Float", storageType);
    }

    [Fact]
    public void AnAbsentValueIsToldApartFromOneWhoseTypeCannotBeRead()
    {
        // The interface calls these "deliberately distinguishable" because they
        // say different things about a schema field. Collapsing them would
        // report a value that is there as one that is not.
        var database = new TweakDB();
        database.Flats.Add("Test.thing.speed", new CFloat());
        var source = Source(database);

        Assert.False(source.TryGetStoredValueType(TweakIdentifier.Of("Test.thing.absent"), out var absent));
        Assert.Null(absent);

        Assert.True(source.TryGetStoredValueType(TweakIdentifier.Of("Test.thing.speed"), out var present));
        Assert.NotNull(present);
    }

    [Fact]
    public void ProvenanceCarriesTheNameAndFingerprintAndNoPath()
    {
        var source = TweakDatabaseSource.From(new TweakDB(), "somefile.bin", "abc123");

        Assert.Equal("somefile.bin", source.Name);
        Assert.Equal("abc123", source.Fingerprint);
        Assert.Contains("somefile.bin", source.Description, StringComparison.Ordinal);
        Assert.Contains("abc123", source.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\", source.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingFileIsRefusedByName()
    {
        var thrown = Assert.Throws<FileNotFoundException>(
            () => TweakDatabaseSource.OpenReadOnly("no-such-database-should-ever-exist.bin"));

        Assert.Contains("no tweak database", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotADatabaseIsRefusedByName()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ripperdoc-not-a-database-{Guid.NewGuid():N}.bin");
        File.WriteAllText(path, "this is not a tweak database");

        try
        {
            var thrown = Assert.Throws<InvalidDataException>(() => TweakDatabaseSource.OpenReadOnly(path));
            Assert.Contains("could not be read as a tweak database", thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AValueWhoseTypeTheModelCannotNameIsUnreadableRatherThanNamedAsSomething()
    {
        // The model answers a type it cannot map with a name that names no
        // storage type. Passed on as a name, it is a type this value was never
        // read to have.
        var database = new TweakDB();
        database.Flats.Add("Test.thing.speed", new ProbeUnmappableElement());

        Assert.True(
            Source(database).TryGetStoredValueType(TweakIdentifier.Of("Test.thing.speed"), out var storageType));
        Assert.Null(storageType);
    }

    [Fact]
    public void SuchAValueLeavesTheFieldUnreadableRatherThanContradicted()
    {
        // The arm the whole check exists for. Contradicted is the strongest
        // thing this engine says - the schema is wrong about this field - and
        // saying it from a type that could not be read would be the manifest
        // making up the one verdict nobody would think to doubt.
        var database = new TweakDB();
        database.Add("Test.thing", new gamedataItem_Record());
        database.Flats.Add("Test.thing.speed", new ProbeUnmappableElement());

        var manifest = ValidationManifest.Build(
            SchemaWith("gamedataItem_Record", "speed", "Float"),
            Source(database));

        var verdict = Assert.Single(manifest.Fields());
        Assert.Equal(ValidationState.StorageTypeUnreadable, verdict.State);
        Assert.Equal(0, verdict.ContradictingValueCount);
        Assert.Null(verdict.ObservedStorageType);
    }

    [Fact]
    public void AFileThatEndsBeforeItsStructureDoesIsRefusedTheSameWay()
    {
        // The other shape of unreadable file. A wrong file is reported by the
        // reader as a code; a file that starts right and stops early ends the
        // reader from inside the parse instead. This method's documentation
        // names one exception for a file that is not a readable database, so
        // the truncated case has to arrive as that one and not as a third
        // thing the caller was never told about.
        var path = Path.Combine(Path.GetTempPath(), $"ripperdoc-truncated-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, HeaderOfAnEmptyDatabase().Take(16).ToArray());

        try
        {
            var thrown = Assert.Throws<InvalidDataException>(() => TweakDatabaseSource.OpenReadOnly(path));

            Assert.Contains("could not be read as a tweak database", thrown.Message, StringComparison.Ordinal);
            Assert.Contains("ends before the structure", thrown.Message, StringComparison.Ordinal);

            // Pinned to the path this covers. If these bytes ever stop ending
            // the reader early - a format version bump would do it - the case
            // has stopped being the one it was written for, and it says so
            // rather than passing on the strength of some other refusal.
            Assert.IsType<EndOfStreamException>(thrown.InnerException);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The library's own bytes for a database with nothing in it, so that a
    /// truncation test starts from a real header rather than from format
    /// constants copied into this file - and carries nothing of the game's.
    /// </summary>
    /// <returns>The written bytes.</returns>
    private static byte[] HeaderOfAnEmptyDatabase()
    {
        using var written = new MemoryStream();
        using (var writer = new TweakDBWriter(written, Encoding.UTF8, true))
        {
            writer.WriteFile(new TweakDB());
        }

        return written.ToArray();
    }

    private static TweakDatabaseSource Source(TweakDB database) =>
        TweakDatabaseSource.From(database, "a database built for this test", "no fingerprint");

    private static RecordSchema SchemaWith(string typeName, string fieldName, string storageType) =>
        RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(
                new[]
                {
                    new RecordTypeShape(
                        typeName,
                        null,
                        true,
                        new[] { new RecordFieldShape(fieldName, storageType) }),
                },
                Array.Empty<DerivationFailure>()),
            "a reading constructed for this test");

    private static RecordSchema EmptySchema() => RecordSchemaDerivation.Derive(
        new RecordTypeSourceReading(
            Array.Empty<RecordTypeShape>(),
            Array.Empty<DerivationFailure>()),
        "a reading constructed for this test");
}
