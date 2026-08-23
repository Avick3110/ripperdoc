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

    [Fact]
    public void APairThatCannotBeAddressedIsCountedRatherThanLosingTheSweep()
    {
        // One record whose name is long enough that a long field name has no
        // identifier at all. The sweep must come back with everything else it
        // established, and say how many places it could not look.
        var schema = SchemaWith(
            Field("speed", "Float"),
            Field(new string('f', 60), "Float"));
        var shipped = new FakeDatabase((new string('r', 200), "gamedataThing_Record"));
        shipped.Store(new string('r', 200), "speed", "Float");

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal(1, manifest.UnaddressableFieldProbes);
        Assert.Equal(1, manifest.StoredValuesExplained);
        Assert.Equal(
            ValidationState.Corroborated,
            manifest.Fields().Single(field => field.FieldName == "speed").State);
    }

    [Fact]
    public void AnUnaddressableSlotIsNotReportedAsOneTheRecordsWereCheckedFor()
    {
        // The two verdicts say different things and only one of them is true
        // here: nothing was checked, because there was no identifier to check
        // under. Reporting a check that did not happen is what this manifest
        // exists to prevent.
        var schema = SchemaWith(Field("speed", "Float"), Field(new string('f', 60), "Float"));
        var shipped = new FakeDatabase((new string('r', 200), "gamedataThing_Record"));
        shipped.Store(new string('r', 200), "speed", "Float");

        var verdict = ValidationManifest.Build(schema, shipped)
            .Fields()
            .Single(field => field.FieldName.Length == 60);

        Assert.Equal(ValidationState.NotAddressable, verdict.State);
        Assert.NotEqual(ValidationState.NoCorroboratingValue, verdict.State);
        Assert.False(verdict.IsValidated);
    }

    [Fact]
    public void AFieldOnAnOrdinaryRecordIsStillReportedAsCheckedAndAbsent()
    {
        // The other arm: where the pair does have an identifier and nothing is
        // stored under it, the verdict must stay the one that says so.
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));

        Assert.Equal(
            ValidationState.NoCorroboratingValue,
            Single(ValidationManifest.Build(schema, shipped)).State);
    }

    [Theory]
    [InlineData("malformed record identifier")]
    [InlineData("field name outside the range")]
    public void NoSingleBadInputCostsTheSweepTheVerdictsItAlreadyReached(string reason)
    {
        // Each reason a pair can lack an identifier, driven through the whole
        // sweep rather than through the arithmetic alone.
        var fieldName = reason == "field name outside the range" ? "caf\u00e9" : "speed";
        var schema = SchemaWith(Field(fieldName, "Float"));

        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        if (reason == "malformed record identifier")
        {
            shipped.AddRawRecord((ulong)(WolvenKit.RED4.Types.TweakDBID)new string('a', 300), "gamedataThing_Record");
        }

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.NotEmpty(manifest.Fields());
        Assert.True(manifest.UnaddressableFieldProbes > 0, "nothing was recorded as unaddressable");
    }

    [Fact]
    public void APairThatCannotBeAddressedIsCountedUnderTheReasonThatApplies()
    {
        // The total said how many; it did not say which, and the three reasons
        // are three different things to go and look at.
        var schema = SchemaWith(Field("caf\u00e9", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal(1, manifest.UnaddressableFieldProbes);
        Assert.Equal(1, manifest.UnaddressableFieldProbesByReason[UnaddressableReason.FieldNameOutsideRange]);
        Assert.Equal(0, manifest.UnaddressableFieldProbesByReason[UnaddressableReason.CombinedNameTooLong]);
        Assert.Equal(0, manifest.UnaddressableFieldProbesByReason[UnaddressableReason.MalformedRecordIdentifier]);
    }

    [Fact]
    public void ALongCombinedNameIsCountedUnderItsOwnReasonAndNotTheOther()
    {
        // The other arm of the same tally, so that neither reason can be the
        // one every pair quietly lands in.
        var schema = SchemaWith(Field(new string('f', 60), "Float"));
        var shipped = new FakeDatabase((new string('r', 200), "gamedataThing_Record"));

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal(1, manifest.UnaddressableFieldProbesByReason[UnaddressableReason.CombinedNameTooLong]);
        Assert.Equal(0, manifest.UnaddressableFieldProbesByReason[UnaddressableReason.FieldNameOutsideRange]);
    }

    [Fact]
    public void TheTallyNamesEveryReasonEvenTheOnesNothingHit()
    {
        // A reason absent from the tally cannot be told apart from a reason
        // that was never checked for.
        var counts = ValidationManifest
            .Build(SchemaWith(Field("speed", "Float")), new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record")))
            .UnaddressableFieldProbesByReason;

        Assert.Equal(3, counts.Count);
        Assert.DoesNotContain(UnaddressableReason.None, counts.Keys);
        Assert.All(counts.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void AnUnaddressableFieldIsCountedOncePerNameItMightBeStoredUnder()
    {
        // The count is of probes, and a field the source offers two spellings of
        // is two probes. Called a count of fields, or of record-and-field pairs,
        // it would read as twice as many fields being unreachable as there are -
        // and the two spellings need not fail together, since one can be short
        // enough to address while the other is not.
        var longEnough = new string('f', 60);
        var schema = SchemaWith(new RecordFieldShape(longEnough, "Float", [longEnough + "Alternate"], null));
        var shipped = new FakeDatabase((new string('r', 200), "gamedataThing_Record"));

        var manifest = ValidationManifest.Build(schema, shipped);

        Assert.Equal(2, manifest.UnaddressableFieldProbes);
        Assert.Equal(2, Single(manifest).Spellings.Count);
        Assert.All(
            Single(manifest).Spellings,
            spelling => Assert.Equal(ValidationState.NotAddressable, spelling.State));
    }

    [Fact]
    public void AnOrdinarySweepAddressesEverythingItLooksAt()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");

        Assert.Equal(0, ValidationManifest.Build(schema, shipped).UnaddressableFieldProbes);
    }

    [Fact]
    public void AFieldConfirmedUnderItsOwnNameIsNotCondemnedByAGuessedSpelling()
    {
        // The case the old field-level tally could not represent. The field is
        // really called speed and shipped values say so; the source also
        // guessed it might be spelled Speed, and something unrelated happens to
        // sit at that identifier. Run together, one value of the wrong type
        // outranks every value of the right one and the field is reported as a
        // slot the schema is wrong about - on evidence about a name it does not
        // have.
        var schema = SchemaWith(new RecordFieldShape("speed", "Float", ["Speed"], null));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");
        shipped.Store("Vehicle.quadra", "Speed", "CName");

        var field = Single(ValidationManifest.Build(schema, shipped));

        Assert.Equal(ValidationState.Corroborated, field.State);
        Assert.Equal(new[] { "speed" }, field.ConfirmedFieldNames);

        // The contradiction is not swept away; it is attributed.
        Assert.Equal(
            ValidationState.Contradicted,
            field.Spellings.Single(spelling => spelling.Name == "Speed").State);
        Assert.Equal("CName", field.Spellings.Single(spelling => spelling.Name == "Speed").ObservedStorageType);

        // And the field-level reading of it stays quiet, because a type named
        // there is read as the type this field's values really have.
        Assert.Null(field.ObservedStorageType);
    }

    [Fact]
    public void AFieldNoSpellingConfirmsIsStillCondemnedByAContradictionUnderAny()
    {
        // The other arm. Nothing has established which spelling is real, so
        // there is no ground for calling the contradiction somebody else's.
        var schema = SchemaWith(new RecordFieldShape("speed", "Float", ["Speed"], null));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "Speed", "CName");

        var field = Single(ValidationManifest.Build(schema, shipped));

        Assert.Equal(ValidationState.Contradicted, field.State);
        Assert.Empty(field.ConfirmedFieldNames);
        Assert.Equal("CName", field.ObservedStorageType);
    }

    [Fact]
    public void OneValueOfTheWrongTypeUnderAFieldsOwnNameStillCondemnsIt()
    {
        // The rule that was there before there were any alternates to weigh,
        // unchanged. Agreement under the same name does not outvote it.
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(
            ("Vehicle.quadra", "gamedataThing_Record"),
            ("Vehicle.other", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");
        shipped.Store("Vehicle.other", "speed", "CName");

        Assert.Equal(ValidationState.Contradicted, Single(ValidationManifest.Build(schema, shipped)).State);
    }

    [Fact]
    public void AFieldIsNotProbedUnderASpellingThatIsAnotherFieldsName()
    {
        // Two fields whose spellings overlap. Probing the guess would take the
        // other field's value as evidence about this one - here, as a
        // contradiction that condemns a field nothing is wrong with.
        var schema = SchemaWith(
            new RecordFieldShape("value", "Float", ["Value"], null),
            new RecordFieldShape("Value", "Int32"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "value", "Float");
        shipped.Store("Vehicle.quadra", "Value", "Int32");

        var manifest = ValidationManifest.Build(schema, shipped);

        var guessing = manifest.Fields().Single(field => field.FieldName == "value");
        Assert.Equal(ValidationState.Corroborated, guessing.State);
        Assert.Equal(new[] { "value" }, guessing.Spellings.Select(spelling => spelling.Name));

        Assert.Equal(
            ValidationState.Corroborated,
            manifest.Fields().Single(field => field.FieldName == "Value").State);
    }

    [Fact]
    public void AFieldTheSourceKnewTheNameOfHasOneSpellingAndOneVerdict()
    {
        var schema = SchemaWith(Field("speed", "Float"));
        var shipped = new FakeDatabase(("Vehicle.quadra", "gamedataThing_Record"));
        shipped.Store("Vehicle.quadra", "speed", "Float");

        var field = Single(ValidationManifest.Build(schema, shipped));
        var spelling = Assert.Single(field.Spellings);

        Assert.Equal("speed", spelling.Name);
        Assert.Equal(field.State, spelling.State);
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

        public void AddRawRecord(ulong identifier, string typeName) =>
            _records.Add(new ShippedRecord(identifier, typeName));

        public bool TryGetStoredValueType(ulong identifier, out string? storageType) =>
            _values.TryGetValue(identifier, out storageType);
    }
}
