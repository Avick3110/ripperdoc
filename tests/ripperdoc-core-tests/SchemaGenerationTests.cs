using Ripperdoc.Core.Dump;
using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The first thing a run does about a generated schema, in each situation it
/// can find itself in.
/// </summary>
/// <remarks>
/// Every sentence this surface prints is a claim to whoever reads it, so each
/// one is checked here rather than trusted: that the situation it names is the
/// one it is in, and that a state nobody could establish is reported as such
/// instead of being rounded to the nearest state that could be.
/// </remarks>
public class SchemaGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ripperdoc-generation-" + Guid.NewGuid().ToString("n"));

    public SchemaGenerationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WithNoSchemaAndNoTypeInformationItSaysWhatIsNeededAndWhyNothingIsShipped()
    {
        var reading = SchemaGeneration.Inspect(new GenerationInputs(ArtifactPath));

        Assert.Equal(GenerationState.NothingToUseAndNothingToGenerateFrom, reading.State);
        Assert.False(reading.ArtifactIsKnownCurrent);

        // The three things a first run has to be told: that it is produced
        // here, that it is not shipped and why, and the two facts that decide
        // whether the copy taken is any good.
        Assert.Contains("on this machine", reading.Explanation, StringComparison.Ordinal);
        Assert.Contains("theirs", reading.Explanation, StringComparison.Ordinal);
        Assert.Contains("before mods are installed", reading.Explanation, StringComparison.Ordinal);
        Assert.Contains("removed", reading.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatWasNamedIsToldApartFromNothingHavingBeenNamed()
    {
        var notADump = Path.Combine(_root, "not-a-dump");
        Directory.CreateDirectory(notADump);

        var reading = SchemaGeneration.Inspect(new GenerationInputs(ArtifactPath, notADump));

        Assert.Equal(GenerationState.NothingToUseAndNothingToGenerateFrom, reading.State);
        Assert.Contains("has no 'classes' in it", reading.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ADumpWithNoClassesInItIsRefusedRatherThanGeneratedFrom()
    {
        using var dump = SyntheticDump.Of();

        var reading = SchemaGeneration.Inspect(new GenerationInputs(ArtifactPath, dump.JsonDirectory));

        Assert.Equal(GenerationState.NothingToUseAndNothingToGenerateFrom, reading.State);
        Assert.Contains("describes no classes at all", reading.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void WithTypeInformationPresentAndNoSchemaItIsReadyToGenerate()
    {
        using var dump = SyntheticDump.Of(classes: [ThingRecord]);

        var reading = SchemaGeneration.Inspect(new GenerationInputs(ArtifactPath, dump.JsonDirectory));

        Assert.Equal(GenerationState.ReadyToGenerate, reading.State);
        Assert.False(reading.ArtifactIsKnownCurrent);
    }

    [Fact]
    public void GeneratingWritesASchemaThatReadsBackAsTheSameSchema()
    {
        using var dump = SyntheticDump.Of(classes: [ThingRecord]);
        var when = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

        var written = SchemaGeneration.Generate(
            new GenerationInputs(ArtifactPath, dump.JsonDirectory),
            shipped: null,
            when);

        var read = SchemaIrDocument.Read(ArtifactPath).ToArtifact();

        Assert.Equal(SchemaMode.GeneratedTypeInformation, read.Provenance.Mode);
        Assert.Equal(when, read.Provenance.GeneratedAt);
        Assert.Equal(written.Records.RecordTypeNames, read.Records.RecordTypeNames);
        Assert.Equal(written.Records.DeclaredFieldCount, read.Records.DeclaredFieldCount);
        Assert.Equal(written.Records.ResolvedFieldSlotCount, read.Records.ResolvedFieldSlotCount);

        // The parts of a field that only the generated mode carries have to
        // survive the round trip, or the artifact quietly becomes the other
        // mode's.
        var field = read.Records.Find("gamedataProbeThing_Record")!.Fields["owner"];
        Assert.Equal("gamedataProbeOther_Record", field.ReferentTypeName);
        Assert.Equal(new[] { "Owner" }, field.AlternateNames);
        Assert.Equal(1, read.References.TypedEdgeCount);
        Assert.Equal(0, read.References.UntypedEdgeCount);
    }

    [Fact]
    public void AnArtifactNothingArbitratedKeepsItsGuessesAndSaysThatIsWhatTheyAre()
    {
        // The other arm of the one below. With no arbiter the candidates are
        // all the schema has, so they are kept - and the artifact says the name
        // it lists a field under is the likelier candidate rather than the one
        // values are keyed by. Unsaid, a consumer finding nothing under it
        // would read that as a field the game does not use.
        using var dump = SyntheticDump.Of(classes: [ThingRecord]);

        var written = SchemaGeneration.Generate(
            new GenerationInputs(ArtifactPath, dump.JsonDirectory),
            shipped: null,
            When);

        Assert.Equal(
            new[] { "Owner" },
            SchemaIrDocument.Read(ArtifactPath).ToArtifact()
                .Records.Find("gamedataProbeThing_Record")!.Fields["owner"].AlternateNames);

        Assert.Contains(
            written.Provenance.NamedLosses,
            loss => loss.Contains("nothing arbitrated between them", StringComparison.Ordinal));
    }

    [Fact]
    public void AnArbitratedArtifactIsWrittenUnderTheNameTheDataConfirmed()
    {
        // What arbitrating buys, made permanent. The source led with 'owner'
        // and real values are keyed by 'Owner'; the artifact records the name
        // the data confirmed, so a later run does not rediscover it, and the
        // guess is not carried forward as though it were still open.
        using var dump = SyntheticDump.Of(classes: [ThingRecord]);
        var shipped = new StubShipped("Vehicle.quadra", "gamedataProbeThing_Record", "Owner", "TweakDBID");

        var written = SchemaGeneration.Generate(
            new GenerationInputs(ArtifactPath, dump.JsonDirectory),
            shipped,
            When);

        // The schema in hand still carries both, because nothing has been
        // written yet and the manifest beside it is what says which won.
        Assert.Equal(new[] { "Owner" }, written.Records.Find("gamedataProbeThing_Record")!.Fields["owner"].AlternateNames);

        var read = SchemaIrDocument.Read(ArtifactPath).ToArtifact();
        var field = read.Records.Find("gamedataProbeThing_Record")!.Fields["Owner"];

        Assert.Equal("Owner", field.Name);
        Assert.Empty(field.AlternateNames);
        Assert.Equal("gamedataProbeOther_Record", field.ReferentTypeName);

        // The verdict travels under the same name as the field it is about.
        Assert.Equal("Owner", Assert.Single(read.Validation!.Fields()).FieldName);
    }

    [Fact]
    public void AnArbiterThatFoundNothingLeavesTheSpellingUndecidedAndSaysSo()
    {
        // Arbitration is not the same as an answer. A database with no value
        // under either candidate decides nothing, so the candidates survive -
        // and the artifact distinguishes that from the case where a spelling
        // was confirmed, rather than reporting a resolved name it has no
        // grounds for.
        using var dump = SyntheticDump.Of(classes: [ThingRecord]);
        var shipped = new StubShipped("Vehicle.quadra", "gamedataProbeThing_Record", null, null);

        var written = SchemaGeneration.Generate(
            new GenerationInputs(ArtifactPath, dump.JsonDirectory),
            shipped,
            When);

        Assert.Equal(
            new[] { "Owner" },
            SchemaIrDocument.Read(ArtifactPath).ToArtifact()
                .Records.Find("gamedataProbeThing_Record")!.Fields["owner"].AlternateNames);

        Assert.Contains(
            written.Provenance.NamedLosses,
            loss => loss.Contains("still undecided", StringComparison.Ordinal));
    }

    /// <summary>One record, and at most one value on it, with nothing of the game's.</summary>
    private sealed class StubShipped(
        string recordName,
        string typeName,
        string? fieldName,
        string? storageType) : IShippedRecordSource
    {
        private readonly ulong _record = TweakIdentifier.Of(recordName);

        public string Description => "a database constructed for this test";

        public int StoredValueCount => fieldName is null ? 0 : 1;

        public IEnumerable<ShippedRecord> Records => [new ShippedRecord(_record, typeName)];

        public bool TryGetStoredValueType(ulong identifier, out string? found)
        {
            found = storageType;

            return fieldName is not null
                && TweakIdentifier.TryForField(_record, fieldName, out var stored, out _)
                && stored == identifier;
        }
    }

    private static readonly DateTimeOffset When = new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AGeneratedSchemaMatchingTheInstalledBuildIsCurrent()
    {
        var reading = InspectWithArtifact(gameBuild: BuildOfThisAssembly, install: PathOfThisAssembly);

        Assert.Equal(GenerationState.ArtifactCurrent, reading.State);
        Assert.True(reading.ArtifactIsKnownCurrent);
    }

    [Fact]
    public void AGeneratedSchemaDescribingAnotherBuildIsReportedAsOutOfDate()
    {
        var reading = InspectWithArtifact(gameBuild: "0.0.0.0", install: PathOfThisAssembly);

        Assert.Equal(GenerationState.ArtifactDescribesAnotherBuild, reading.State);
        Assert.False(reading.ArtifactIsKnownCurrent);
        Assert.Contains("out of date", reading.Explanation, StringComparison.Ordinal);
        Assert.Contains("0.0.0.0", reading.Explanation, StringComparison.Ordinal);
        Assert.Contains(BuildOfThisAssembly, reading.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoInstallToCompareAgainstStalenessIsReportedAsUncheckedAndNotAsCurrent()
    {
        var reading = InspectWithArtifact(gameBuild: BuildOfThisAssembly, install: null);

        Assert.Equal(GenerationState.StalenessCannotBeChecked, reading.State);
        Assert.False(reading.ArtifactIsKnownCurrent);
        Assert.Contains("cannot be checked", reading.Explanation, StringComparison.Ordinal);
        Assert.Contains("not the same as it having been found current", reading.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AGeneratedSchemaThatRecordedNoBuildCannotBeCalledCurrentEither()
    {
        var reading = InspectWithArtifact(gameBuild: null, install: PathOfThisAssembly);

        Assert.Equal(GenerationState.StalenessCannotBeChecked, reading.State);
        Assert.False(reading.ArtifactIsKnownCurrent);
        Assert.Contains("does not record which build", reading.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInstallPathWithNothingAtItIsNotMistakenForABuild()
    {
        var reading = InspectWithArtifact(
            gameBuild: BuildOfThisAssembly,
            install: Path.Combine(_root, "no-game-here.exe"));

        Assert.Equal(GenerationState.StalenessCannotBeChecked, reading.State);
        Assert.Null(reading.InstalledBuild);
    }

    [Fact]
    public void AnUnreadableSchemaIsNamedRatherThanTreatedAsAbsentAndOverwritten()
    {
        File.WriteAllText(ArtifactPath, "{ this is not a schema artifact ");

        var reading = SchemaGeneration.Inspect(new GenerationInputs(ArtifactPath));

        Assert.Equal(GenerationState.ArtifactUnreadable, reading.State);
        Assert.Contains("Nothing has been written over it", reading.Explanation, StringComparison.Ordinal);

        // The claim in that sentence, checked rather than asserted.
        Assert.Equal("{ this is not a schema artifact ", File.ReadAllText(ArtifactPath));
    }

    [Fact]
    public void ASchemaWrittenInAnotherFormatIsRefusedRatherThanReadAsThisOne()
    {
        File.WriteAllText(
            ArtifactPath,
            """
            {"formatVersion":99,"mode":"GeneratedTypeInformation","typeInformationSource":"x",
             "generatedAt":"2026-08-23T00:00:00+00:00","types":[],"derivationFailures":[]}
            """);

        var thrown = Assert.Throws<InvalidDataException>(() => SchemaIrDocument.Read(ArtifactPath));

        Assert.Contains("format 99", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASchemaNamingAModeThisBuildDoesNotKnowIsRefused()
    {
        File.WriteAllText(
            ArtifactPath,
            """
            {"formatVersion":1,"mode":"SomethingElse","typeInformationSource":"x",
             "generatedAt":"2026-08-23T00:00:00+00:00","types":[],"derivationFailures":[]}
            """);

        var thrown = Assert.Throws<InvalidDataException>(() => SchemaIrDocument.Read(ArtifactPath).ToArtifact());

        Assert.Contains("SomethingElse", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratingFromTypeInformationWithABrokenChainIsRefusedBeforeAnySchemaIsBuilt()
    {
        // A chain that leaves the type information part way through resolves to
        // a shorter field set. Caught before generating rather than reported
        // afterwards, because the artifact would otherwise be written and be
        // quietly short.
        using var dump = SyntheticDump.Of(classes:
        [
            """{"name":"gamedataProbeThing_Record","parent":"gamedataProbeAbsent_Record","flags":66}""",
        ]);

        var thrown = Assert.Throws<InvalidOperationException>(() => SchemaGeneration.Generate(
            new GenerationInputs(ArtifactPath, dump.JsonDirectory),
            shipped: null,
            DateTimeOffset.UnixEpoch));

        Assert.Contains("gamedataProbeAbsent_Record", thrown.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(ArtifactPath), "nothing should have been written");
    }

    [Fact]
    public void GeneratingWithNothingToGenerateFromIsRefusedAndSaysWhatIsNeeded()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => SchemaGeneration.Generate(
            new GenerationInputs(ArtifactPath),
            shipped: null,
            DateTimeOffset.UnixEpoch));

        Assert.Contains("on this machine", thrown.Message, StringComparison.Ordinal);
    }

    private GenerationReading InspectWithArtifact(string? gameBuild, string? install)
    {
        using var dump = SyntheticDump.Of(classes: [ThingRecord]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, "type information authored for this test");
        var reading = new DumpRecordTypeSource(model).Read();
        var schema = RecordSchemaDerivation.Derive(reading, model.Description);
        var artifact = SchemaIr.Create(schema, null, SchemaMode.GeneratedTypeInformation, DateTimeOffset.UnixEpoch);

        SchemaIrDocument.Of(artifact, reading, gameBuild).Write(ArtifactPath);

        return SchemaGeneration.Inspect(new GenerationInputs(ArtifactPath, dump.JsonDirectory, install));
    }

    private string ArtifactPath => Path.Combine(_root, "schema.json");

    /// <summary>
    /// A real file with a real version on it, standing in for the game's own.
    /// </summary>
    /// <remarks>
    /// The check under test reads a build number off a file. Using this
    /// assembly means the fixture exercises the real reading rather than a
    /// value handed straight back, and it works on any machine.
    /// </remarks>
    private static string PathOfThisAssembly => typeof(SchemaGenerationTests).Assembly.Location;

    private static string BuildOfThisAssembly =>
        System.Diagnostics.FileVersionInfo.GetVersionInfo(PathOfThisAssembly).FileVersion!;

    private const string ThingRecord =
        """
        {"name":"gamedataProbeThing_Record","flags":66,
         "funcs":[{"fullName":"Owner","shortName":"Owner","flags":1,
                   "return":{"type":"whandle:gamedataProbeOther_Record","flags":64}}]}
        """;
}
