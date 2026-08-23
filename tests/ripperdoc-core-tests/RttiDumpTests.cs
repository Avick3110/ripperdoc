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

        Model = DumpTypeModel.Load(path, GeneratedDescription);
        Schema = RecordSchemaDerivation.Derive(new DumpRecordTypeSource(Model));
        Graph = ReferenceGraph.Of(Schema);

        var compiled = TypeModelReading.FromPinnedTypeModel();
        Audit = TypeModelAudit.Run(TypeModelReading.From(Model), compiled.Reading);
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

    public DumpTypeModel Model { get; }

    public RecordSchema Schema { get; }

    public ReferenceGraph Graph { get; }

    public TypeModelAudit Audit { get; }

    public DriftReceipt Receipt { get; }
}
