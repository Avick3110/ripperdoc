using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The whole spine, run against a real shipped database.
/// </summary>
/// <remarks>
/// <para>
/// Tier (ii): this needs game data that is not this project's to redistribute,
/// so it cannot run on a runner and does not try to. The gate script runs it
/// when the environment names a database and announces it as skipped, by name,
/// when nothing does. Run outside the gate with no database named, it fails
/// rather than passing quietly.
/// </para>
/// <para>
/// The counts below are what the research this port reproduces measured. A
/// divergence is a defect in the port, and the way to close it is to find out
/// which values moved and why - never to move the number written here to
/// whatever the code now produces.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.ShippedDatabase)]
public class ShippedDatabaseValidationTests : IClassFixture<ShippedDatabaseFixture>
{
    private const int RecordsInTheDatabase = 193_354;
    private const int ValuesInTheDatabase = 3_306_462;
    private const int ValuesTheSchemaExplains = 3_150_037;
    private const int FieldSlotsCorroborated = 6_584;
    private const int FieldSlotsNoValueCarriesThem = 13;
    private const int FieldSlotsOnTypesWithNoRecords = 658;

    private readonly ShippedDatabaseFixture _fixture;

    public ShippedDatabaseValidationTests(ShippedDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheDatabaseAnswersWhetherTwoValuesAreTheSameValue()
    {
        // This predicate decides whether a record the shipped data copied still
        // follows what it was copied from, and therefore whether a write to the
        // source moves it. Everywhere else it is exercised through a hand-built
        // stand-in whose comparison shares no code with this one, so without
        // this check the real implementation runs only where nothing asserts on
        // it.
        var source = _fixture.Database;

        var values = source.Records
            .Select(record => record.Identifier)
            .SelectMany(identifier => new[] { "name", "displayName", "entityName" }
                .Select(field => TweakIdentifier.TryForField(identifier, field, out var flat) ? flat : 0UL))
            .Where(identifier => identifier != 0 && source.HoldsValue(identifier))
            .Take(2)
            .ToArray();

        Assert.Equal(2, values.Length);

        // A value is the same value as itself, and absence is not agreement -
        // an identifier the database does not hold agrees with nothing, because
        // treating it as equal would propagate a value onto a record that has
        // no such value at all.
        Assert.True(source.ValuesMatch(values[0], values[0]));
        Assert.False(source.ValuesMatch(values[0], 0UL));
        Assert.False(source.ValuesMatch(0UL, 0UL));

        Assert.True(source.HoldsRecord(source.Records.First().Identifier));
        Assert.False(source.HoldsRecord(0UL));
    }

    [Fact]
    public void TheDatabaseHoldsWhatThisPortWasMeasuredAgainst()
    {
        // The database's identity is not asserted here: the fixture refuses to
        // build against any other one, so an assertion at this point could
        // never fail and would read as a check while being none.
        Assert.Equal(RecordsInTheDatabase, _fixture.Manifest.RecordsExamined);
        Assert.Equal(ValuesInTheDatabase, _fixture.Manifest.StoredValueCount);
        Assert.Equal(0, _fixture.Manifest.UnaddressableFieldProbes);
    }

    [Fact]
    public void TheSchemaExplainsTheShippedValuesItWasMeasuredToExplain()
    {
        Assert.Equal(ValuesTheSchemaExplains, _fixture.Manifest.StoredValuesExplained);
        Assert.Equal(0.9527d, Math.Round(_fixture.Manifest.ExplainedShare, 4));
    }

    [Fact]
    public void EveryRecordTypeInTheDatabaseIsOneTheSchemaKnows()
    {
        Assert.Empty(_fixture.Manifest.RecordTypesNotInSchema);
    }

    [Fact]
    public void NoFieldIsContradictedByWhatTheDatabaseActuallyStores()
    {
        // Every value the schema explains is stored as the type the schema
        // claims for it. This is the check that would catch the inherited type
        // model having drifted from the game in a way that matters, and it is
        // the strongest single statement the no-setup mode can make about
        // itself.
        var counts = _fixture.Manifest.StateCounts();

        Assert.Equal(0, counts[ValidationState.Contradicted]);
        Assert.Equal(0, counts[ValidationState.StorageTypeUnreadable]);
    }

    [Fact]
    public void EveryFieldSlotIsMarkedAndTheMarksAccountForAllOfThem()
    {
        var counts = _fixture.Manifest.StateCounts();

        Assert.Equal(FieldSlotsCorroborated, counts[ValidationState.Corroborated]);
        Assert.Equal(FieldSlotsNoValueCarriesThem, counts[ValidationState.NoCorroboratingValue]);
        Assert.Equal(FieldSlotsOnTypesWithNoRecords, counts[ValidationState.NoShippedRecordsOfType]);
        Assert.Equal(0, counts[ValidationState.NotAddressable]);
        Assert.Equal(_fixture.Schema.ResolvedFieldSlotCount, counts.Values.Sum());
        Assert.Equal(_fixture.Schema.ResolvedFieldSlotCount, _fixture.Manifest.Fields().Count);
    }

    [Fact]
    public void TheArtifactSaysWhichModeMadeItAndWhatArbitratedIt()
    {
        var artifact = _fixture.Artifact;

        Assert.Equal(SchemaMode.InheritedTypeModel, artifact.Provenance.Mode);
        Assert.NotNull(artifact.Validation);
        Assert.Contains(ShippedDatabaseFixture.MeasuredDatabase, artifact.Provenance.ValidatedAgainst!, StringComparison.Ordinal);

        // A provenance block travels wherever the artifact is pasted, so it
        // carries a fingerprint of the database rather than the place it was
        // found on this machine.
        Assert.DoesNotContain(":\\", artifact.Provenance.ValidatedAgainst!, StringComparison.Ordinal);
        Assert.NotEmpty(artifact.Provenance.NamedLosses);
    }

    [Fact]
    public void TheSweepNoticesWhenTheSchemaItIsCheckingIsDamaged()
    {
        // The canary. A sweep that always returns the same number tells you
        // nothing about whether it ran, so one field the database really does
        // carry is renamed and the sweep is expected to come back worse. If
        // this passes, the machinery discriminates; if it does not, every other
        // number in this file is unearned.
        var busiest = _fixture.Manifest.Fields()
            .OrderByDescending(field => field.CorroboratingValueCount)
            .ThenBy(field => field.FieldName, StringComparer.Ordinal)
            .First();

        Assert.True(busiest.CorroboratingValueCount > 0, "the undamaged sweep corroborated nothing");

        var damaged = ValidationManifest.Build(
            RecordSchemaDerivation.Derive(
                Damage(_fixture.Reading, busiest.DeclaringTypeName, busiest.FieldName),
                "the pinned type model with one field renamed"),
            _fixture.Database);

        Assert.True(
            damaged.StoredValuesExplained < _fixture.Manifest.StoredValuesExplained,
            $"renaming '{busiest.FieldName}' explained {damaged.StoredValuesExplained} values, "
            + $"no fewer than the {_fixture.Manifest.StoredValuesExplained} the intact schema explains");

        var afterwards = damaged.Fields()
            .Single(field => field.RecordTypeName == busiest.RecordTypeName
                && field.FieldName == busiest.FieldName + DamageSuffix);

        Assert.Equal(ValidationState.NoCorroboratingValue, afterwards.State);
    }

    private const string DamageSuffix = "_renamed_by_the_canary";

    private static RecordTypeSourceReading Damage(
        RecordTypeSourceReading reading,
        string typeName,
        string fieldName)
    {
        var types = reading.Types
            .Select(type => type.TypeName == typeName
                ? type with
                {
                    DeclaredFields = type.DeclaredFields
                        .Select(field => field.FieldName == fieldName
                            ? field with { FieldName = field.FieldName + DamageSuffix }
                            : field)
                        .ToArray(),
                }
                : type)
            .ToArray();

        return reading with { Types = types };
    }
}

/// <summary>
/// The shipped database, parsed once for every check that needs it.
/// </summary>
public sealed class ShippedDatabaseFixture
{
    public ShippedDatabaseFixture()
    {
        var path = Environment.GetEnvironmentVariable(VariableName);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"These checks read a shipped tweak database, which no runner has. Set {VariableName} to one "
                + "to run them. The gate script announces them as skipped, by name, when it cannot run them - "
                + "an absent database is never reported as a pass.");
        }

        Database = TweakDatabaseSource.OpenReadOnly(path);

        // Which database this is decides whether the counts below mean
        // anything. Two builds of the game ship differently sized databases
        // under sibling names in the same directory, so pointing at the wrong
        // one would otherwise fail every count with a message blaming this
        // port for a divergence that is really a different game build.
        if (!string.Equals(Database.Fingerprint, MeasuredDatabase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"These checks reproduce counts measured against {MeasuredDatabaseDescription}, whose sha256 "
                + $"is {MeasuredDatabase}. {VariableName} names '{Database.Name}', whose sha256 is "
                + $"{Database.Fingerprint}. That is a different database, so the counts do not apply to it - "
                + "this is not a defect in the engine. Point the variable at the same build, or measure the "
                + "counts afresh against yours and say in the check which build they came from.");
        }

        Reading = ReflectedRecordTypeSource.FromPinnedTypeModel().Read();
        Schema = RecordSchemaDerivation.Derive(Reading, "the pinned type model");
        Manifest = ValidationManifest.Build(Schema, Database);
        Artifact = SchemaIr.Create(Schema, Manifest, SchemaMode.InheritedTypeModel, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The environment variable naming the database, derived from the brand
    /// rather than spelled out, so a rebrand does not leave a stale name here.
    /// </summary>
    public static string VariableName => Branding.Name.ToUpperInvariant() + "_TWEAKDB_PATH";

    /// <summary>
    /// The SHA-256 of the one database the counts in these checks were measured
    /// against.
    /// </summary>
    /// <remarks>
    /// Read from the file the gate script also reads, so that the gate's
    /// decision to run or skip this tier and these checks' own guard can never
    /// disagree about which database the counts belong to.
    /// </remarks>
    public static string MeasuredDatabase { get; } = File
        .ReadAllText(Path.Combine(AppContext.BaseDirectory, "measured-database.sha256"))
        .Trim();

    /// <summary>Which game build that database belongs to, in words.</summary>
    public const string MeasuredDatabaseDescription = "game 2.31 with Phantom Liberty";

    public TweakDatabaseSource Database { get; }

    public RecordTypeSourceReading Reading { get; }

    public RecordSchema Schema { get; }

    public ValidationManifest Manifest { get; }

    public SchemaIr Artifact { get; }
}
