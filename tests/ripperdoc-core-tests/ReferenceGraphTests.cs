using Ripperdoc.Core.Schema;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The graph of which record types point at which, and what real values say
/// about it.
/// </summary>
public class ReferenceGraphTests
{
    private const string Source = "a reading constructed for this test";

    [Fact]
    public void AFieldStoringAnIdentifierIsAnEdgeAndOtherFieldsAreNot()
    {
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            Reference("owner", "gamedataProbeOther_Record"),
            new RecordFieldShape("speed", "Float")));

        var edge = Assert.Single(graph.Edges);

        Assert.Equal("gamedataProbeThing_Record", edge.RecordTypeName);
        Assert.Equal("owner", edge.FieldName);
        Assert.Equal("gamedataProbeOther_Record", edge.ReferentTypeName);
        Assert.False(edge.IsSequence);
    }

    [Fact]
    public void AFieldStoringAListOfIdentifiersIsOneEdgeThatSaysItIsAList()
    {
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            new RecordFieldShape("parts", "array:TweakDBID", [], "gamedataProbeOther_Record")));

        Assert.True(Assert.Single(graph.Edges).IsSequence);
    }

    [Fact]
    public void AnEdgeWithNoReferentIsCountedAsTheShortfallItIs()
    {
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            new RecordFieldShape("owner", "TweakDBID")));

        Assert.Equal(1, graph.UntypedEdgeCount);
        Assert.Equal(0, graph.TypedEdgeCount);
        Assert.Empty(graph.ReferentTypeNames);
    }

    [Fact]
    public void AnInheritedReferenceIsAnEdgeOnTheTypeThatInheritsIt()
    {
        var graph = GraphOf(
            Type("gamedataProbeBase_Record", null, Reference("owner", "gamedataProbeOther_Record")),
            Type("gamedataProbeThing_Record", "gamedataProbeBase_Record"));

        var inherited = Assert.Single(graph.From("gamedataProbeThing_Record"));

        Assert.Equal("owner", inherited.FieldName);
        Assert.Equal("gamedataProbeBase_Record", inherited.DeclaringTypeName);
    }

    [Fact]
    public void AKindDerivingFromThePermittedOneIsPermitted()
    {
        var graph = GraphOf(
            Type("gamedataProbeBase_Record", null),
            Type("gamedataProbeDerived_Record", "gamedataProbeBase_Record"),
            Type("gamedataProbeSibling_Record", null));

        Assert.True(graph.Permits("gamedataProbeBase_Record", "gamedataProbeBase_Record"));
        Assert.True(graph.Permits("gamedataProbeBase_Record", "gamedataProbeDerived_Record"));

        // The other direction is not permitted: a record of the base kind does
        // not carry what a field wanting the derived kind expects to find.
        Assert.False(graph.Permits("gamedataProbeDerived_Record", "gamedataProbeBase_Record"));
        Assert.False(graph.Permits("gamedataProbeBase_Record", "gamedataProbeSibling_Record"));
    }

    [Fact]
    public void AReferentTheSchemaDoesNotKnowIsNamedRatherThanCounted()
    {
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            Reference("owner", "gamedataProbeAbsent_Record")));

        Assert.Equal(new[] { "gamedataProbeAbsent_Record" }, graph.UnresolvedReferentTypeNames());
    }

    [Fact]
    public void AReferenceNamingThePermittedKindCorroboratesTheEdge()
    {
        var check = Check(
            GraphOf(
                Type("gamedataProbeThing_Record", null, Reference("owner", "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeOther_Record")
                .PointingFrom(1, "owner", 2));

        Assert.Equal(1, check.ReferencesFollowed);
        Assert.Equal(1, check.ReferencesOfPermittedKind);
        Assert.Equal(0, check.ReferencesOfOtherKind);
        Assert.True(check.NothingContradictsTheGraph);
    }

    [Fact]
    public void AReferenceNamingAnotherKindContradictsTheEdgeAndIsNamed()
    {
        var check = Check(
            GraphOf(
                Type("gamedataProbeThing_Record", null, Reference("owner", "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null),
                Type("gamedataProbeElse_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeElse_Record")
                .PointingFrom(1, "owner", 2));

        Assert.Equal(1, check.ReferencesOfOtherKind);
        Assert.False(check.NothingContradictsTheGraph);

        var example = Assert.Single(check.Examples);
        Assert.Equal("gamedataProbeOther_Record", example.PermittedTypeName);
        Assert.Equal("gamedataProbeElse_Record", example.ActualTypeName);
    }

    [Fact]
    public void AReferenceNamingNoRecordIsCountedApartFromOneNamingTheWrongKind()
    {
        var check = Check(
            GraphOf(
                Type("gamedataProbeThing_Record", null, Reference("owner", "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .PointingFrom(1, "owner", 999));

        Assert.Equal(1, check.ReferencesToNothing);
        Assert.Equal(0, check.ReferencesOfOtherKind);
        Assert.True(check.NothingContradictsTheGraph);
    }

    [Fact]
    public void AnUntypedEdgeIsReportedAsUncheckedRatherThanAsHavingPassed()
    {
        var check = Check(
            GraphOf(Type("gamedataProbeThing_Record", null, new RecordFieldShape("owner", "TweakDBID"))),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .PointingFrom(1, "owner", 1));

        Assert.Equal(1, check.UntypedEdgesNotChecked);
        Assert.Equal(0, check.TypedEdgesChecked);
        Assert.Equal(0, check.ReferencesFollowed);
    }

    [Fact]
    public void AValueTheSchemaCallsAReferenceAndTheDataStoresOtherwiseIsCounted()
    {
        var check = Check(
            GraphOf(
                Type("gamedataProbeThing_Record", null, Reference("owner", "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .HoldingSomethingElseAt(1, "owner"));

        Assert.Equal(1, check.ValuesUnreadable);
        Assert.Equal(0, check.ValuesRead);
    }

    [Fact]
    public void TheInheritedModeProducesReferencesAndCanTypeNoneOfThem()
    {
        // The one number that states the cost of inheriting a type model rather
        // than generating one, checked against the model the engine is pinned
        // to. It needs no game and no dump: the pinned model is reflectable
        // wherever this runs.
        var graph = ReferenceGraph.Of(
            RecordSchemaDerivation.Derive(ReflectedRecordTypeSource.FromPinnedTypeModel()));

        Assert.Equal(2095, graph.Edges.Count);
        Assert.Equal(0, graph.TypedEdgeCount);
        Assert.Equal(2095, graph.UntypedEdgeCount);
        Assert.Empty(graph.ReferentTypeNames);
    }

    private static ReferenceGraph GraphOf(params RecordTypeShape[] types) =>
        ReferenceGraph.Of(RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(types, Array.Empty<DerivationFailure>()),
            Source));

    private static ReferenceValidation Check(ReferenceGraph graph, SyntheticReferenceSource source) =>
        ReferenceValidation.Build(graph, source, source);

    private static RecordTypeShape Type(string name, string? baseName, params RecordFieldShape[] fields) =>
        new(name, baseName, true, fields);

    private static RecordFieldShape Reference(string name, string referent) =>
        new(name, "TweakDBID", [], referent);
}

/// <summary>
/// Looking a field up under a spelling the schema did not choose as its
/// primary one.
/// </summary>
/// <remarks>
/// Its own class because it is not about the reference graph. It is about the
/// consequence of a source carrying more than one candidate name for one field:
/// every caller that asks "does this type have this field" has to ask in a way
/// that sees the alternates, or a field the schema really does have reads as
/// one it lacks.
/// </remarks>
public class RecordFieldLookupTests
{
    [Fact]
    public void AFieldIsFoundUnderItsPrimaryNameAndUnderItsAlternates()
    {
        var type = TypeWith(new RecordFieldShape("steamKey", "CName", ["SteamKey"], null));

        Assert.Equal("steamKey", type.FindField("steamKey")!.Name);
        Assert.Equal("steamKey", type.FindField("SteamKey")!.Name);
        Assert.Null(type.FindField("somethingElse"));
    }

    [Fact]
    public void AFieldWithNoAlternatesIsFoundOnlyUnderItsOwnName()
    {
        var type = TypeWith(new RecordFieldShape("speed", "Float"));

        Assert.NotNull(type.FindField("speed"));
        Assert.Null(type.FindField("Speed"));
    }

    [Fact]
    public void AnAlternateThatIsAnotherFieldsRealNameDoesNotDisplaceIt()
    {
        // Two fields whose spellings overlap: one is really called Value and
        // the other offers Value as a guess at how its own name is spelled.
        // The one that is really called that is the better answer.
        var type = TypeWith(
            new RecordFieldShape("value", "Float", ["Value"], null),
            new RecordFieldShape("Value", "Int32"));

        Assert.Equal("Int32", type.FindField("Value")!.StorageType);
        Assert.Equal("Float", type.FindField("value")!.StorageType);
    }

    private static RecordType TypeWith(params RecordFieldShape[] fields) =>
        RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(
                [new RecordTypeShape("gamedataProbeThing_Record", null, true, fields)],
                Array.Empty<DerivationFailure>()),
            "a reading constructed for this test")
            .Find("gamedataProbeThing_Record")!;
}
