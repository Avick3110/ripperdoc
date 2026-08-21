using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The manifest, against databases constructed in memory.
/// </summary>
/// <remarks>
/// The real arbiter is a file this project may not redistribute, so the cases
/// that matter - a field the data confirms, a field the data contradicts, a
/// field nothing could have confirmed - are built here from nothing. Zero
/// game-derived bytes are involved, which is why these run anywhere.
/// </remarks>
public class ValidationManifestTests
{
    [Fact]
    public void AFieldRealDataCarriesIsValidated()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");

        var verdict = Single(ValidationManifest.Build(schema, shipped));

        Assert.True(verdict.IsValidated);
        Assert.Equal(ValidationState.Corroborated, verdict.State);
        Assert.Equal(1, verdict.CorroboratingValueCount);
        Assert.Null(verdict.ObservedStorageType);
    }

    [Fact]
    public void AFieldStoredAsAnotherTypeIsContradictedRatherThanValidated()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "CName");

        var verdict = Single(ValidationManifest.Build(schema, shipped));

        Assert.False(verdict.IsValidated);
        Assert.Equal(ValidationState.Contradicted, verdict.State);
        Assert.Equal("CName", verdict.ObservedStorageType);
    }

    [Fact]
    public void OneContradictionIsNotOutvotedByAgreementsElsewhere()
    {
        // A schema field is wrong about a type or it is not. A single stored
        // value of the wrong type is the finding; the ones that happened to
        // match do not average it away.
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(
            ("Vehicle.quadra", "gamedataThing_Record"),
            ("Vehicle.caliburn", "gamedataThing_Record"),
            ("Vehicle.porsche", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");
        shipped.Store("Vehicle.caliburn", "speed", "Float");
        shipped.Store("Vehicle.porsche", "speed", "Int32");

        var verdict = Single(ValidationManifest.Build(schema, shipped));

        Assert.Equal(ValidationState.Contradicted, verdict.State);
        Assert.Equal(2, verdict.CorroboratingValueCount);
        Assert.Equal(1, verdict.ContradictingValueCount);
        Assert.Equal("Int32", verdict.ObservedStorageType);
    }

    [Fact]
    public void AFieldNoRecordCarriesIsUnvalidatedAndSaysWhy()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));

        var verdict = Single(ValidationManifest.Build(schema, shipped));

        Assert.False(verdict.IsValidated);
        Assert.Equal(ValidationState.NoCorroboratingValue, verdict.State);
    }

    [Fact]
    public void AFieldOnATypeWithNoRecordsIsToldApartFromOneNothingCarries()
    {
        // Nothing confirmed either field, and the reasons are not the same
        // thing: one type was looked at and did not carry it, the other was
        // never there to look at.
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase();

        var verdict = Single(ValidationManifest.Build(schema, shipped));

        Assert.Equal(ValidationState.NoShippedRecordsOfType, verdict.State);
    }

    [Fact]
    public void AValueWhoseTypeCannotBeReadIsNeitherConfirmedNorDenied()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", storageType: null);

        var verdict = Single(ValidationManifest.Build(schema, shipped));

        Assert.False(verdict.IsValidated);
        Assert.Equal(ValidationState.StorageTypeUnreadable, verdict.State);
    }

    [Fact]
    public void ARecordTypeTheSchemaDoesNotKnowIsNamedRatherThanCountedAsResidue()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(
            ("Vehicle.quadra", "gamedataThing_Record"),
            ("Mod.something", "gamedataInventedByAMod_Record"));

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal(new[] { "gamedataInventedByAMod_Record" }, manifest.RecordTypesNotInSchema);
        Assert.Equal(2, manifest.RecordsExamined);
    }

    [Fact]
    public void ExplainedValuesAreCountedAgainstEverythingTheDatabaseHolds()
    {
        var schema = SchemaWith(Field("speed", "Float"), Field("mass", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");
        shipped.StoreUnattached("Loose.value");
        shipped.StoreUnattached("Loose.other");
        shipped.StoreUnattached("Loose.third");

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal(1, manifest.StoredValuesExplained);
        Assert.Equal(4, manifest.StoredValueCount);
        Assert.Equal(0.25d, manifest.ExplainedShare);
    }

    [Fact]
    public void EveryStateIsCountedIncludingTheOnesNothingIsIn()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");

        var counts = ValidationManifest.Build(schema, shipped).StateCounts();

        Assert.Equal(Enum.GetValues<ValidationState>().Length, counts.Count);
        Assert.Equal(1, counts[ValidationState.Corroborated]);
        Assert.Equal(0, counts[ValidationState.Contradicted]);
    }

    [Fact]
    public void InheritedFieldsAreCheckedOnEveryTypeThatCarriesThem()
    {
        var schema = RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(
                new[]
                {
                    new RecordTypeShape("gamedataBase_Record", null, true, new[] { Field("shared", "Float") }),
                    new RecordTypeShape("gamedataThing_Record", "gamedataBase_Record", true, Array.Empty<RecordFieldShape>()),
                },
                Array.Empty<DerivationFailure>()),
            "a reading constructed for this test");

        var shipped = new FakeDatabase(("Thing.one", "gamedataThing_Record"));
        shipped.Store("Thing.one", "shared", "Float");

        var manifest = ValidationManifest.Build(schema, shipped);
        var onThing = manifest.Fields().Single(field => field.RecordTypeName == "gamedataThing_Record");

        Assert.Equal(ValidationState.Corroborated, onThing.State);
        Assert.Equal("gamedataBase_Record", onThing.DeclaringTypeName);

        // The same field on the base type has nothing of its own to confirm it.
        var onBase = manifest.Fields().Single(field => field.RecordTypeName == "gamedataBase_Record");
        Assert.Equal(ValidationState.NoShippedRecordsOfType, onBase.State);
    }

    [Fact]
    public void ValidatedAndUnvalidatedPartitionTheManifest()
    {
        var schema = SchemaWith(Field("speed", "Float"), Field("mass", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal("speed", Assert.Single(manifest.Validated()).FieldName);
        Assert.Equal("mass", Assert.Single(manifest.Unvalidated()).FieldName);
        Assert.Equal(manifest.Fields().Count, manifest.Validated().Count() + manifest.Unvalidated().Count());
    }

    private static FieldValidation Single(ValidationManifest manifest) => Assert.Single(manifest.Fields());

    private static RecordFieldShape Field(string name, string storageType) => new(name, storageType);

    private static RecordSchema SchemaWith(params RecordFieldShape[] fields) =>
        RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(
                new[] { new RecordTypeShape("gamedataThing_Record", null, true, fields) },
                Array.Empty<DerivationFailure>()),
            "a reading constructed for this test");

    /// <summary>A database built from names, with nothing of the game's in it.</summary>
    private sealed class FakeDatabase : IShippedRecordSource
    {
        private readonly List<ShippedRecord> _records = new();
        private readonly Dictionary<ulong, string?> _values = new();

        public FakeDatabase(params (string Name, string TypeName)[] records)
        {
            foreach (var (name, typeName) in records)
            {
                _records.Add(new ShippedRecord(TweakIdentifier.Of(name), typeName));
            }
        }

        public string Description => "a database constructed for this test";

        public int StoredValueCount => _values.Count;

        public IEnumerable<ShippedRecord> Records => _records;

        public void Store(string recordName, string fieldName, string? storageType) =>
            _values[TweakIdentifier.ForField(TweakIdentifier.Of(recordName), fieldName)] = storageType;

        public void StoreUnattached(string name) => _values[TweakIdentifier.Of(name)] = "Float";

        public bool TryGetStoredValueType(ulong identifier, out string? storageType) =>
            _values.TryGetValue(identifier, out storageType);
    }
}
