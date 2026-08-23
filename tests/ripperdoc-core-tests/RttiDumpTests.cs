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
    private const int ValuesInTheDatabase = 3_306_462;
    private const int ValuesTheGeneratedSchemaExplains = 3_150_040;
    private const int SlotsTheDataContradicts = 100;

    private readonly RttiDumpFixture _fixture;

    public RttiDumpTests(RttiDumpFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheGeneratedInformationIsReadWholeAndNothingInItIsUnread()
    {
        Assert.Equal(ClassesInTheDump, _fixture.Model.Classes.Count);
        Assert.Empty(_fixture.Model.UnrecognisedKeys);
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
    public void EveryTypedEdgeSurvivesBeingCheckedAgainstTheReferencesReallyStored()
    {
        // The graph claims a kind of record for each reference. Followed over
        // the whole database, almost every stored reference agrees; the few
        // that do not are the game's own data putting an unrelated kind in a
        // field, which is worth reporting and is not the graph being wrong.
        var check = _fixture.ReferenceCheck;

        Assert.Equal(0, check.UntypedEdgesNotChecked);
        Assert.True(check.ReferencesFollowed > 1_000_000, $"only {check.ReferencesFollowed} were followed");

        var resolvable = check.ReferencesOfPermittedKind + check.ReferencesOfOtherKind;
        Assert.True(
            check.ReferencesOfOtherKind * 10_000 < resolvable,
            $"{check.ReferencesOfOtherKind} of {resolvable} resolvable references name a kind the schema "
            + "does not permit there, which is more than the shipped data was measured to do");
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
        // somebody makes deliberately.
        var produced = _fixture.ProducedReceipt.ToJson();
        var producedPath = Path.Combine(AppContext.BaseDirectory, DriftReceipt.ProducedFileName);
        File.WriteAllText(producedPath, produced);

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
        // The fingerprint above would still match if the audit compared far
        // less and happened to find the same divergences among what it did
        // look at.
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
        var counts = _fixture.Audit.CountsByKind();

        Assert.Equal(0, counts[DivergenceKind.ParentDiffers]);
        Assert.Equal(0, counts[DivergenceKind.PropertyAbsentFromModel]);
        Assert.Equal(0, counts[DivergenceKind.PropertyTypeDiffers]);
        Assert.Equal(0, counts[DivergenceKind.EnumAbsentFromModel]);
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
        Schema = RecordSchemaDerivation.Derive(new DumpRecordTypeSource(Model));
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
    }

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
}
