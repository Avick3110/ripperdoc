using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The artifact, and the honesty of its provenance block.
/// </summary>
/// <remarks>
/// A degraded mode that does not say what it lost is indistinguishable from a
/// complete one until it gives a wrong answer, so what the artifact says about
/// itself is a behaviour worth testing rather than documentation worth writing.
/// </remarks>
public class SchemaIrTests
{
    private static readonly DateTimeOffset When = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheArtifactNamesItsModeAndWhereItsTypeInformationCameFrom()
    {
        var artifact = SchemaIr.Create(Schema(), null, SchemaMode.InheritedTypeModel, When);

        Assert.Equal(SchemaMode.InheritedTypeModel, artifact.Provenance.Mode);
        Assert.Equal("a reading constructed for this test", artifact.Provenance.TypeInformationSource);
        Assert.Equal(When, artifact.Provenance.GeneratedAt);
    }

    [Fact]
    public void AnUnarbitratedSchemaSaysSoRatherThanLookingConfirmed()
    {
        var artifact = SchemaIr.Create(Schema(), null, SchemaMode.InheritedTypeModel, When);

        Assert.Null(artifact.Validation);
        Assert.Null(artifact.Provenance.ValidatedAgainst);
        Assert.Contains(artifact.Provenance.NamedLosses, loss => loss.Contains("No shipped data arbitrated", StringComparison.Ordinal));
    }

    [Fact]
    public void AnArbitratedSchemaNamesWhatArbitratedIt()
    {
        var artifact = SchemaIr.Create(Schema(), Manifest(), SchemaMode.InheritedTypeModel, When);

        Assert.NotNull(artifact.Validation);
        Assert.Equal("a database constructed for this test", artifact.Provenance.ValidatedAgainst);
        Assert.DoesNotContain(artifact.Provenance.NamedLosses, loss => loss.Contains("No shipped data arbitrated", StringComparison.Ordinal));
    }

    [Fact]
    public void TheInheritedModeNamesTheThreeThingsItCannotDo()
    {
        var losses = SchemaIr.Create(Schema(), Manifest(), SchemaMode.InheritedTypeModel, When)
            .Provenance.NamedLosses;

        Assert.Contains(losses, loss => loss.Contains("not for kind", StringComparison.Ordinal));
        Assert.Contains(losses, loss => loss.Contains("Drift between the type model", StringComparison.Ordinal));
        Assert.Contains(losses, loss => loss.Contains("newly patched game", StringComparison.Ordinal));
    }

    [Fact]
    public void TheReferenceEdgeCountInTheLossIsComputedFromTheSchemaItDescribes()
    {
        // Quoting a number from a document is how an artifact ends up stating
        // a count that stopped being true two versions ago.
        var losses = SchemaIr.Create(Schema(), null, SchemaMode.InheritedTypeModel, When)
            .Provenance.NamedLosses;

        Assert.Contains(losses, loss => loss.Contains("3 field slots store a record identifier", StringComparison.Ordinal));
    }

    [Fact]
    public void ReferenceFieldsAreCountedThroughAnyDepthOfArray()
    {
        Assert.Equal(3, SchemaIr.ReferenceFieldCount(Schema()));
    }

    [Fact]
    public void ARecordTypeTheSchemaDoesNotCoverIsANamedLoss()
    {
        var shipped = new StubDatabase(("Mod.thing", "gamedataInventedByAMod_Record"));
        var manifest = ValidationManifest.Build(Schema(), shipped);

        var losses = SchemaIr.Create(Schema(), manifest, SchemaMode.InheritedTypeModel, When)
            .Provenance.NamedLosses;

        Assert.Contains(losses, loss => loss.Contains("absent from this schema", StringComparison.Ordinal));
    }

    [Fact]
    public void TheModeSpecificLossesAreClaimedOnlyByTheModeTheyBelongTo()
    {
        var losses = SchemaIr.Create(Schema(), Manifest(), SchemaMode.GeneratedTypeInformation, When)
            .Provenance.NamedLosses;

        Assert.DoesNotContain(losses, loss => loss.Contains("Drift between the type model", StringComparison.Ordinal));
        Assert.DoesNotContain(losses, loss => loss.Contains("newly patched game", StringComparison.Ordinal));

        // The reference-edge shortfall is a fact about this schema rather than
        // about the mode, so it survives the mode changing.
        Assert.Contains(losses, loss => loss.Contains("not for kind", StringComparison.Ordinal));
    }

    [Fact]
    public void PairsWithNoIdentifierAtAllAreANamedLossInTheArtifact()
    {
        // A count that reaches the manifest and stops there tells nobody. The
        // artifact is what a consumer holds, so the loss has to arrive in it.
        var schema = RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(
                new[]
                {
                    new RecordTypeShape("gamedataThing_Record", null, true, new[]
                    {
                        new RecordFieldShape(new string('f', 60), "Float"),
                    }),
                },
                Array.Empty<DerivationFailure>()),
            "a reading constructed for this test");

        var shipped = new StubDatabase((new string('r', 200), "gamedataThing_Record"));
        var manifest = ValidationManifest.Build(schema, shipped);
        Assert.True(manifest.UnaddressableFieldProbes > 0, "the sweep recorded nothing unaddressable");

        var losses = SchemaIr.Create(schema, manifest, SchemaMode.InheritedTypeModel, When)
            .Provenance.NamedLosses;

        Assert.Contains(losses, loss => loss.Contains("no identifier at", StringComparison.Ordinal));
    }

    [Fact]
    public void ASweepThatCouldLookEverywhereClaimsNoSuchLoss()
    {
        var losses = SchemaIr.Create(Schema(), Manifest(), SchemaMode.InheritedTypeModel, When)
            .Provenance.NamedLosses;

        Assert.DoesNotContain(losses, loss => loss.Contains("no identifier at", StringComparison.Ordinal));
    }

    private static RecordSchema Schema() => RecordSchemaDerivation.Derive(
        new RecordTypeSourceReading(
            new[]
            {
                new RecordTypeShape("gamedataThing_Record", null, true, new[]
                {
                    new RecordFieldShape("speed", "Float"),
                    new RecordFieldShape("points", "TweakDBID"),
                    new RecordFieldShape("relatives", "array:TweakDBID"),
                    new RecordFieldShape("nested", "array:array:TweakDBID"),
                    new RecordFieldShape("label", "CName"),
                }),
            },
            Array.Empty<DerivationFailure>()),
        "a reading constructed for this test");

    private static ValidationManifest Manifest() =>
        ValidationManifest.Build(Schema(), new StubDatabase(("Thing.one", "gamedataThing_Record")));

    private sealed class StubDatabase(params (string Name, string TypeName)[] records) : IShippedRecordSource
    {
        public string Description => "a database constructed for this test";

        public int StoredValueCount => 0;

        public IEnumerable<ShippedRecord> Records => records
            .Select(record => new ShippedRecord(TweakIdentifier.Of(record.Name), record.TypeName));

        public bool TryGetStoredValueType(ulong identifier, out string? storageType)
        {
            storageType = null;
            return false;
        }
    }
}
