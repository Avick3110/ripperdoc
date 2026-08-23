using Ripperdoc.Core.Drift;
using Ripperdoc.Core.Dump;
using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The half of the drift gate that needs type information generated from a game
/// install, and the dump-bound schema it also makes possible.
/// </summary>
/// <remarks>
/// <para>
/// Tier (iii): generated type information comes from the user's own install and
/// is not this project's to redistribute, so this cannot run on a runner and
/// does not try to. The gate script runs it when the environment names a dump
/// and announces it as skipped, by name, when nothing does. Run outside the
/// gate with nothing named, it fails rather than passing quietly.
/// </para>
/// <para>
/// The counts here are what the research this port reproduces measured. A
/// divergence is a defect in the port, and the way to close it is to find out
/// what moved and why - never to move the number written here to whatever the
/// code now produces.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.RttiDump)]
public class RttiDumpTests : IClassFixture<RttiDumpFixture>
{
    private const int ClassesInTheDump = 15_830;
    private const int RecordTypes = 965;
    private const int DeclaredFields = 4_796;
    private const int FieldsCarryingAReferent = 1_234;
    private const int DistinctReferentTypes = 490;
    private const int TypedEdges = 2_085;
    private const int ValuesInTheDatabase = 3_306_462;
    private const int ValuesTheGeneratedSchemaExplains = 3_150_039;
    private const int ValuesTheInheritedSchemaExplains = 3_150_037;
    private const int ValuesReachedOnlyUnderARejectedSpelling = 1;
    private const int ValuesReachedOnlyUnderAnUnvindicatedSpelling = 67_272;
    private const int SlotsTheDataContradicts = 66;
    private const int SlotsSomeSpellingContradicts = 100;
    private const int SlotsCondemnedOnlyByAGuessedSpelling = 34;
    private const int SlotsConfirmedOnlyUnderTheAccessorSpelling = 89;
    private const int SlotsConfirmedUnderBothSpellings = 8;
    private const int EdgesChecked = 1_517_882;
    private const int ReferencesFollowed = 2_300_852;
    private const int ReferencesOfPermittedKind = 1_915_600;
    private const int ReferencesOfOtherKind = 59;
    private const int ReferencesToNothing = 385_193;
    private const int ValuesUnreadableUnderATypedEdge = 8;

    private readonly RttiDumpFixture _fixture;

    public RttiDumpTests(RttiDumpFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheGeneratedInformationIsReadWholeAndNothingInItIsUnread()
    {
        Assert.Equal(ClassesInTheDump, _fixture.Model.Classes.Count);
        Assert.Empty(_fixture.Model.UnrecognisedKeys);

        // A count alone would be satisfied by a dump that described 15,830
        // types under 15,829 usable names. This says the reader lost none of
        // them - no name shared, no description without one.
        Assert.Empty(_fixture.Model.ReadFailures);
    }

    [Fact]
    public void TheDerivedSchemaReproducesTheMeasuredRecordSurface()
    {
        Assert.Equal(RecordTypes, _fixture.Schema.RecordTypeNames.Count);
        Assert.Equal(DeclaredFields, _fixture.Schema.DeclaredFieldCount);
        Assert.Empty(_fixture.Schema.Failures);
    }

    [Fact]
    public void EveryReferenceInTheDerivedSchemaSaysWhatKindOfRecordItPointsAt()
    {
        // How many there are, pinned rather than left to be inferred. Every
        // other published figure is asserted somewhere and this one was not, so
        // a derivation that quietly stopped producing a few hundred edges would
        // have left this check green: "all of them are typed" is true of a
        // smaller set too.
        Assert.Equal(TypedEdges, _fixture.Graph.Edges.Count);

        // The one capability the inherited mode structurally cannot have. If
        // this ever reports untyped edges, the generated mode has stopped
        // buying the thing it costs a generation step to buy.
        Assert.Equal(0, _fixture.Graph.UntypedEdgeCount);
        Assert.Equal(_fixture.Graph.Edges.Count, _fixture.Graph.TypedEdgeCount);
        Assert.Equal(DistinctReferentTypes, _fixture.Graph.ReferentTypeNames.Count);

        // An edge pointing at a kind of record the schema has never heard of
        // would be a claim nothing could check.
        Assert.Empty(_fixture.Graph.UnresolvedReferentTypeNames());

        var carryingAReferent = _fixture.Schema.RecordTypeNames
            .Select(_fixture.Schema.Find)
            .SelectMany(type => type!.DeclaredFields.Values)
            .Count(field => field.ReferentTypeName is not null);

        Assert.Equal(FieldsCarryingAReferent, carryingAReferent);
    }

    [Fact]
    public void TheGeneratedSchemaExplainsWhatTheResearchMeasuredItExplaining()
    {
        // The number this whole mode exists to reproduce as product code. It is
        // measured over the same shipped database the research used, and the
        // arbiter reaches it by arithmetic over names rather than by any table,
        // so a divergence here is a defect in the derivation and not a
        // difference of opinion about what counts.
        Assert.Equal(ValuesInTheDatabase, _fixture.Manifest.StoredValueCount);
        Assert.Equal(ValuesTheGeneratedSchemaExplains, _fixture.Manifest.StoredValuesExplained);
        Assert.Empty(_fixture.Manifest.RecordTypesNotInSchema);
        Assert.Equal(0, _fixture.Manifest.UnaddressableFieldProbes);
    }

    [Fact]
    public void ProbingASecondNamePerFieldBuysAlmostNoneOfTheCoverage()
    {
        // What the coverage number would have been under the two other rules it
        // could have been counted by, measured rather than argued. The published
        // law rests on the gap between them being negligible, and a law resting
        // on a number nothing reproduces is a law nobody can check.
        //
        // Counted here rather than read off the manifest, because the manifest
        // deliberately reports one of these three and the finding quotes all of
        // them.
        var everyCandidate = new HashSet<ulong>();
        var vindicatedOnly = new HashSet<ulong>();
        var confirmedBySlot = _fixture.Manifest.Fields().ToDictionary(
            field => (field.RecordTypeName, field.FieldName),
            field => field.ConfirmedFieldNames.ToArray());

        foreach (var record in _fixture.Database.Records)
        {
            if (_fixture.Schema.Find(record.TypeName) is not { } type)
            {
                continue;
            }

            foreach (var field in type.Fields.Values)
            {
                var confirmed = confirmedBySlot.GetValueOrDefault((type.Name, field.Name)) ?? [];

                foreach (var candidate in type.Spellings.Of(field))
                {
                    if (!TweakIdentifier.TryForField(record.Identifier, candidate, out var identifier, out _)
                        || !_fixture.Database.TryGetStoredValueType(identifier, out _))
                    {
                        continue;
                    }

                    everyCandidate.Add(identifier);

                    if (confirmed.Contains(candidate, StringComparer.Ordinal))
                    {
                        vindicatedOnly.Add(identifier);
                    }
                }
            }
        }

        // The widest possible probe, against what the schema is actually keyed
        // by once the arbiter has spoken. One value between them.
        Assert.Equal(
            ValuesReachedOnlyUnderARejectedSpelling,
            everyCandidate.Count - _fixture.Manifest.StoredValuesExplained);

        // And how much of the widest probe rests on a spelling no shipped value
        // ever vindicated, which is the size of the old artifact rather than of
        // any disagreement between the two modes.
        Assert.Equal(
            ValuesReachedOnlyUnderAnUnvindicatedSpelling,
            everyCandidate.Count - vindicatedOnly.Count);

        // The whole of the generated mode's margin over the inherited one, which
        // is what the finding leads with. Two values in three and a quarter
        // million; both modes round to the same share to four decimal places.
        Assert.Equal(
            2,
            _fixture.Manifest.StoredValuesExplained - ValuesTheInheritedSchemaExplains);
    }

    [Fact]
    public void TheSchemaSaysOutLoudThatShippedValuesContradictSomeOfIt()
    {
        // The generated mode is not strictly better than the inherited one, and
        // the artifact is required to say so rather than let a reader assume a
        // schema derived from the game is right about everything.
        var contradicted = _fixture.Manifest.Fields()
            .Count(field => field.State == ValidationState.Contradicted);

        Assert.Equal(SlotsTheDataContradicts, contradicted);
        Assert.Contains(
            _fixture.Artifact.Provenance.NamedLosses,
            loss => loss.Contains("contradicted by shipped values", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldIsNotCondemnedForWhatSitsAtASpellingTheDataRejected()
    {
        // The whole return on tallying per spelling, on real data. A slot where
        // some spelling holds a value of the wrong type is not thereby a slot
        // the schema is wrong about: where another spelling holds values of the
        // right type, the data has said which name is the field's and the
        // wrong-typed value belongs to whatever else that identifier addresses.
        //
        // These three numbers have to add up, and the check says so rather than
        // asserting each alone - two of them moving together in the wrong
        // direction would leave three separate assertions all green.
        var fields = _fixture.Manifest.Fields();

        var anySpellingContradicts = fields
            .Count(field => field.Spellings.Any(s => s.State == ValidationState.Contradicted));
        var rescued = fields.Count(field =>
            field.State == ValidationState.Corroborated
            && field.Spellings.Any(s => s.State == ValidationState.Contradicted));
        var condemned = fields.Count(field => field.State == ValidationState.Contradicted);

        Assert.Equal(SlotsSomeSpellingContradicts, anySpellingContradicts);
        Assert.Equal(SlotsCondemnedOnlyByAGuessedSpelling, rescued);
        Assert.Equal(SlotsTheDataContradicts, condemned);
        Assert.Equal(anySpellingContradicts, rescued + condemned);
    }

    [Fact]
    public void TheDataDecidesWhichSpellingOfAFieldNameIsReal()
    {
        // The source cannot recover a name's capitalisation, so it offers both
        // and the arbiter settles it. These say the settling actually happens
        // and in both directions - a schema where the source's first guess were
        // always right would not need the alternates at all, and one where it
        // were never right would mean the guess is wrong rather than uncertain.
        var multi = _fixture.Manifest.Fields().Where(field => field.Spellings.Count > 1).ToArray();

        Assert.Equal(
            SlotsConfirmedOnlyUnderTheAccessorSpelling,
            multi.Count(field => field.ConfirmedFieldNames.Any()
                && !field.ConfirmedFieldNames.Contains(field.FieldName, StringComparer.Ordinal)));
        Assert.Equal(
            SlotsConfirmedUnderBothSpellings,
            multi.Count(field => field.ConfirmedFieldNames.Count() > 1));
    }

    [Fact]
    public void EveryTypedEdgeSurvivesBeingCheckedAgainstTheReferencesReallyStored()
    {
        // The graph claims a kind of record for each reference. Followed over
        // the whole database, almost every stored reference agrees; the few
        // that do not are the game's own data putting an unrelated kind in a
        // field, which is worth reporting and is not the graph being wrong.
        //
        // The numbers are the ones the finding publishes, not bounds around
        // them. Both inputs are pinned - the database by its hash, the type
        // information by the fingerprint of what the source read out of it - so
        // there is one answer here and a bound would only be hiding which of
        // these five moved.
        var check = _fixture.ReferenceCheck;

        Assert.Equal(0, check.UntypedEdgesNotChecked);
        Assert.Equal(EdgesChecked, check.TypedEdgesChecked);

        // The two ways a pair or a record can contribute to none of the counts
        // below. Both expected empty here, and both asserted rather than
        // assumed: a sweep that quietly stopped addressing pairs, or met records
        // of a type the schema had lost, would otherwise report a smaller
        // examination as an equally clean one.
        Assert.Equal(0, check.PairsNotAddressable);
        Assert.Empty(check.RecordTypesNotInSchema);
        Assert.Equal(ReferencesFollowed, check.ReferencesFollowed);
        Assert.Equal(ReferencesOfPermittedKind, check.ReferencesOfPermittedKind);
        Assert.Equal(ReferencesOfOtherKind, check.ReferencesOfOtherKind);
        Assert.Equal(ReferencesToNothing, check.ReferencesToNothing);
        Assert.Equal(ValuesUnreadableUnderATypedEdge, check.ValuesUnreadable);

        // And they add up. Five counts asserted apart could each be right while
        // the run followed references it never accounted for.
        Assert.Equal(
            check.ReferencesFollowed,
            check.ReferencesOfPermittedKind + check.ReferencesOfOtherKind + check.ReferencesToNothing);
    }

    [Fact]
    public void TheFirstRunPathGeneratesAnArtifactThatReadsBackAsTheSameSchema()
    {
        // The generated mode end to end, on the real thing: produce the
        // artifact through the path a first run takes, read it back, and check
        // that what comes back is the schema that went in rather than a
        // narrower copy of it.
        var artifactPath = Path.Combine(Path.GetTempPath(), "ripperdoc-tier3-" + Guid.NewGuid().ToString("n") + ".json");

        try
        {
            var written = SchemaGeneration.Generate(
                new GenerationInputs(artifactPath, _fixture.DumpDirectory),
                _fixture.Database,
                DateTimeOffset.UnixEpoch);

            var read = SchemaIrDocument.Read(artifactPath).ToArtifact();

            Assert.Equal(SchemaMode.GeneratedTypeInformation, read.Provenance.Mode);
            Assert.Equal(written.Records.RecordTypeNames, read.Records.RecordTypeNames);
            Assert.Equal(written.Records.ResolvedFieldSlotCount, read.Records.ResolvedFieldSlotCount);
            Assert.Equal(ValuesTheGeneratedSchemaExplains, read.Validation!.StoredValuesExplained);
            Assert.Equal(written.References.TypedEdgeCount, read.References.TypedEdgeCount);
            Assert.Equal(0, read.References.UntypedEdgeCount);
            Assert.Equal(written.Provenance.NamedLosses, read.Provenance.NamedLosses);
        }
        finally
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }
        }
    }

    [Fact]
    public void TheAuditFindsExactlyTheDivergencesThatWereAccepted()
    {
        if (ComparisonCannotRun())
        {
            return;
        }

        var audit = _fixture.Audit;
        var receipt = _fixture.Receipt;

        Assert.True(
            string.Equals(audit.DivergenceFingerprint, receipt.DivergenceFingerprint, StringComparison.Ordinal),
            $"The audit found {audit.Divergences.Count} divergence(s) whose fingerprint is "
            + $"{audit.DivergenceFingerprint}; the accepted set's is {receipt.DivergenceFingerprint}. The two "
            + "descriptions of the game now disagree in a way nobody has looked at. Read the divergences on "
            + "this machine before accepting them - they are not in the repository, deliberately.");
    }

    [Fact]
    public void TheReceiptThisRunTakesIsTheReceiptThatWasAccepted()
    {
        // Every field of the receipt at once, compared as the text the file
        // holds. Checking the fields this run cares about one at a time would
        // leave whichever ones no check names asserted by nothing at all - and
        // a committed record with unasserted fields in it is a record that can
        // drift from the run it claims to describe without anything noticing.
        //
        // What this run produced is written out beside the test binaries rather
        // than over the committed file, so accepting a new result is a copy
        // somebody makes deliberately. The fixture writes it whatever the
        // reading was, so the file is there to copy even on a run that could
        // not compare.
        var produced = _fixture.ProducedReceipt.ToJson();
        var producedPath = Path.Combine(AppContext.BaseDirectory, DriftReceipt.ProducedFileName);

        if (ComparisonCannotRun())
        {
            return;
        }

        Assert.True(
            string.Equals(File.ReadAllText(DriftReceiptTests.ReceiptPath).ReplaceLineEndings("\n"), produced,
                StringComparison.Ordinal),
            "The receipt this run takes is not the one that was accepted. What the audit found, or what it "
            + "was run against, has moved. Read the divergences on this machine before accepting them - they "
            + $"are not in the repository, deliberately - and then copy '{producedPath}' over "
            + $"'tests/{DriftReceipt.FileName}'.");
    }

    [Fact]
    public void TheAuditComparedWhatTheAcceptedResultSaysItCompared()
    {
        if (ComparisonCannotRun())
        {
            return;
        }

        // The fingerprint above would still match if the audit compared far
        // less and happened to find the same divergences among what it did
        // look at.
        //
        // Guarded like its neighbours. These four counts happen to be
        // insensitive to the instability as it has been measured - a property
        // that is present on both sides and given a foreign stored type on one
        // - but that is what has been seen and not what the reading can do. One
        // that adds or drops a class, property, enumeration or member moves
        // these counts, and a process reading differently would then redden a
        // healthy tree while the gate printed a skip for the same run.
        Assert.Equal(_fixture.Receipt.ClassesCompared, _fixture.Audit.ClassesCompared);
        Assert.Equal(_fixture.Receipt.PropertiesCompared, _fixture.Audit.PropertiesCompared);
        Assert.Equal(_fixture.Receipt.EnumsCompared, _fixture.Audit.EnumsCompared);
        Assert.Equal(_fixture.Receipt.EnumMembersCompared, _fixture.Audit.EnumMembersCompared);
    }

    [Fact]
    public void TheAcceptedResultNamesTheGeneratedInformationThisRunRead()
    {
        // The receipt's statement about its other input, checked where that
        // input actually is. A machine with no dump takes this on trust because
        // it has nothing to compare against; this one does not have to.
        Assert.Equal(
            _fixture.Receipt.GeneratedTypeInformationFingerprint,
            _fixture.GeneratedReading.Fingerprint());
    }

    [Fact]
    public void TheGameRegistersNoTypeOrPropertyTheModelIsMissing()
    {
        // The categories the research measured as empty, asserted separately
        // from the fingerprint so that a red run says which kind of drift
        // appeared rather than only that something did. These are the kinds
        // that change what this engine reads; the ones the accepted set does
        // carry are texture and rendering entries that no lane here touches.
        if (ComparisonCannotRun())
        {
            return;
        }

        var counts = _fixture.Audit.CountsByKind();

        Assert.Equal(0, counts[DivergenceKind.ParentDiffers]);
        Assert.Equal(0, counts[DivergenceKind.PropertyAbsentFromModel]);
        Assert.Equal(0, counts[DivergenceKind.PropertyTypeDiffers]);
        Assert.Equal(0, counts[DivergenceKind.EnumAbsentFromModel]);
    }

    /// <summary>
    /// Whether this process can say anything about drift at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reflecting the pinned model does not give the same answer in every
    /// process. A run that read it differently from the run the accepted result
    /// came out of is holding the game's description against a different
    /// opposite number, and what it finds is a property of this process rather
    /// than of the two descriptions.
    /// </para>
    /// <para>
    /// Returning rather than failing, because a healthy tree reading
    /// differently is not a defect and a gate that reddened on it would be
    /// switched off inside a week. Returning rather than passing quietly
    /// either: the run writes down which of the two it did, and the gate reads
    /// that and reports a skip by name. What is asserted here is that the
    /// writing happened and says the right thing - so a broken report cannot
    /// leave a comparison that never ran looking like one that passed.
    /// </para>
    /// <para>
    /// Interim, pending the question filed about the instability itself.
    /// </para>
    /// </remarks>
    private bool ComparisonCannotRun()
    {
        if (_fixture.ReadingMatchesPin)
        {
            return false;
        }

        var status = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, DriftReceipt.AuditStatusFileName));

        Assert.NotEqual(DriftReceipt.AuditRanStatus, status);
        Assert.Contains(_fixture.CompiledReadingFingerprint, status, StringComparison.Ordinal);
        Assert.Contains(_fixture.Receipt.TypeModelReadingFingerprint, status, StringComparison.Ordinal);
        Assert.Contains(DriftReceipt.InterimPinStatus, status, StringComparison.Ordinal);

        return true;
    }
}

/// <summary>
/// The generated type information, read once for every check that needs it.
/// </summary>
public sealed class RttiDumpFixture
{
    public RttiDumpFixture()
    {
        var path = Environment.GetEnvironmentVariable(VariableName);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"These checks read type information generated from a game install, which no runner has. Set "
                + $"{VariableName} to a dump's json directory to run them. The gate script announces them as "
                + "skipped, by name, when it cannot run them - an absent dump is never reported as a pass.");
        }

        var databasePath = Environment.GetEnvironmentVariable(ShippedDatabaseFixture.VariableName);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new InvalidOperationException(
                $"These checks arbitrate the generated schema against a shipped database as well as reading "
                + $"generated type information. Set {ShippedDatabaseFixture.VariableName} to one to run them. "
                + "A schema checked against nothing is not a schema anything confirmed.");
        }

        DumpDirectory = path;
        Model = DumpTypeModel.Load(path, GeneratedDescription);

        var reading = new DumpRecordTypeSource(Model).Read();

        // The counts below were measured against one body of type information,
        // as they were against one build of the database. Another one is a
        // different input rather than a defect here, and is announced as such
        // instead of failing counts with a message blaming the engine for
        // somebody else's dump.
        //
        // Fingerprinted over what the source read rather than over the files:
        // what these counts depend on is the shapes, so type information that
        // reads the same way is the same input here even if the files differ.
        if (!string.Equals(reading.Fingerprint(), MeasuredTypeInformation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"These checks reproduce counts measured against type information whose reading "
                + $"fingerprints as {MeasuredTypeInformation}. {VariableName} names one that reads as "
                + $"{reading.Fingerprint()}. That is different type information, so the counts do not apply "
                + "to it - this is not a defect in the engine.");
        }

        Schema = RecordSchemaDerivation.Derive(reading, Model.Description);
        Graph = ReferenceGraph.Of(Schema);

        Database = TweakDatabaseSource.OpenReadOnly(databasePath);

        // The counts below were measured against one build of the game. Another
        // one is a different input rather than a defect here, and is announced
        // as such instead of failing every count with a message blaming the
        // engine for somebody else's file.
        if (!string.Equals(Database.Fingerprint, ShippedDatabaseFixture.MeasuredDatabase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"These checks reproduce counts measured against "
                + $"{ShippedDatabaseFixture.MeasuredDatabaseDescription}, whose sha256 is "
                + $"{ShippedDatabaseFixture.MeasuredDatabase}. {ShippedDatabaseFixture.VariableName} names "
                + $"'{Database.Name}', whose sha256 is {Database.Fingerprint}. That is a different database, "
                + "so the counts do not apply to it - this is not a defect in the engine.");
        }

        Manifest = ValidationManifest.Build(Schema, Database);
        Artifact = SchemaIr.Create(Schema, Manifest, SchemaMode.GeneratedTypeInformation, DateTimeOffset.UnixEpoch);
        ReferenceCheck = ReferenceValidation.Build(Graph, Database, Database);

        var compiled = TypeModelReading.FromPinnedTypeModel();
        GeneratedReading = TypeModelReading.From(Model);
        Audit = TypeModelAudit.Run(GeneratedReading, compiled.Reading);
        ProducedReceipt = DriftReceipt.Of(Audit, compiled, GeneratedReading);
        Receipt = DriftReceipt.Read(DriftReceiptTests.ReceiptPath);

        // Which of the readings this one build of the dependency can produce
        // this process got. Settled once per process and stable afterwards, so
        // there is nothing to be gained by reading it again here - what varies
        // is which process you are in.
        CompiledReadingFingerprint = compiled.Reading.Fingerprint();
        ReadingMatchesPin = string.Equals(
            CompiledReadingFingerprint,
            Receipt.TypeModelReadingFingerprint,
            StringComparison.Ordinal);

        // The produced receipt is written whatever the reading was. Writing it
        // is not accepting it, and a run that refused to write one after a
        // dependency bump would leave no way to accept a new result at all -
        // the pin in the committed receipt belongs to the old build, so every
        // run would mismatch and every run would decline to produce the file
        // that fixes it.
        File.WriteAllText(
            Path.Combine(AppContext.BaseDirectory, DriftReceipt.ProducedFileName),
            ProducedReceipt.ToJson());

        File.WriteAllText(
            Path.Combine(AppContext.BaseDirectory, DriftReceipt.AuditStatusFileName),
            ReadingMatchesPin ? DriftReceipt.AuditRanStatus : PinMismatchReason());
    }

    /// <summary>
    /// Why the comparison did not run, in the words the gate repeats.
    /// </summary>
    /// <remarks>
    /// Which property this process read differently is not named, and cannot be
    /// from anything committed: telling would need the pinned reading's
    /// contents, and those are a description of the game's own types, which this
    /// repository does not carry. What can be pointed at is the run's own
    /// output, which is on the machine that has the dump and nowhere else.
    /// </remarks>
    private string PinMismatchReason() =>
        "this process read the pinned dependency's type model as "
        + $"{CompiledReadingFingerprint}, and the accepted audit was taken against "
        + $"{Receipt.TypeModelReadingFingerprint}. Reflecting the model does not give the same answer in "
        + "every process - a few properties take a foreign stored type, differently each time - so what this "
        + "run would find is not drift and is not reported as any. The comparison is not run. The receipt "
        + $"records this pin as '{DriftReceipt.InterimPinStatus}'; run the gate again to get a process that "
        + "reads as the pin does, and read the divergences on this machine - they are not in the repository, "
        + "deliberately.";

    /// <summary>
    /// The environment variable naming the dump, derived from the brand rather
    /// than spelled out, so a rebrand does not leave a stale name here.
    /// </summary>
    public static string VariableName => Branding.Name.ToUpperInvariant() + "_RTTI_DUMP_PATH";

    /// <summary>
    /// How the generated information describes itself in anything it produces.
    /// </summary>
    /// <remarks>
    /// Names the game build and never the machine path it was read from. A path
    /// in an artifact is a path in everything the artifact is pasted into.
    /// </remarks>
    public const string GeneratedDescription = "RTTI dump of game 2.31 with Phantom Liberty";

    /// <summary>
    /// The type information every count in these checks was measured against,
    /// as the fingerprint of what the dump-bound source reads out of it.
    /// </summary>
    /// <remarks>
    /// A hash of a thing rather than the thing, so it may live here. It is the
    /// counterpart of the database's own fingerprint: without it the counts
    /// below are pinned on one side only, and a run against different type
    /// information would report the engine as wrong about numbers that were
    /// never measured for it.
    /// </remarks>
    public const string MeasuredTypeInformation =
        "c5e3c582d6573957063fbe65e8c6378f56b6bd7161a2fa7b89f54278b2e0f602";

    /// <summary>Where the generated type information was read from.</summary>
    public string DumpDirectory { get; }

    public DumpTypeModel Model { get; }

    public RecordSchema Schema { get; }

    public ReferenceGraph Graph { get; }

    public TweakDatabaseSource Database { get; }

    public ValidationManifest Manifest { get; }

    public SchemaIr Artifact { get; }

    public ReferenceValidation ReferenceCheck { get; }

    /// <summary>The generated type information in the audit's vocabulary.</summary>
    public TypeModelReading GeneratedReading { get; }

    public TypeModelAudit Audit { get; }

    /// <summary>The receipt this run takes, which the committed one must equal.</summary>
    public DriftReceipt ProducedReceipt { get; }

    public DriftReceipt Receipt { get; }

    /// <summary>How this process read the pinned dependency's type model.</summary>
    public string CompiledReadingFingerprint { get; }

    /// <summary>
    /// Whether that is the reading the accepted audit was taken against, and
    /// therefore whether comparing this run's divergences to the accepted set
    /// says anything about drift.
    /// </summary>
    public bool ReadingMatchesPin { get; }
}
