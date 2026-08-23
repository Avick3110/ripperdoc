using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
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
    public void AKindNotInTheSchemaIsPermittedOnlyByBeingThePermittedKind()
    {
        // A chain resolved at construction has nothing for a name the schema
        // never carried, and the answer for one is the same as walking from it
        // would have given: it is where the walk starts and where it ends.
        var graph = GraphOf(Type("gamedataProbeBase_Record", null));

        Assert.True(graph.Permits("gamedataProbeStranger_Record", "gamedataProbeStranger_Record"));
        Assert.False(graph.Permits("gamedataProbeBase_Record", "gamedataProbeStranger_Record"));
    }

    [Fact]
    public void AskingWhetherAKindIsPermittedAllocatesNothing()
    {
        // The claim the resolved chains exist to make. This is asked once per
        // stored reference - millions of times against a real database - and a
        // set allocated per question is the shape of cost this engine has
        // already published a measurement about. A counter of calls would not
        // catch a regression here; what regresses is the work each call does,
        // so that is what is measured.
        var graph = GraphOf(
            Type("gamedataProbeBase_Record", null),
            Type("gamedataProbeMiddle_Record", "gamedataProbeBase_Record"),
            Type("gamedataProbeDerived_Record", "gamedataProbeMiddle_Record"));

        // Warmed first, so what is measured is the asking and not the
        // just-in-time compilation of the code that asks.
        for (var i = 0; i < 100; i++)
        {
            graph.Permits("gamedataProbeBase_Record", "gamedataProbeDerived_Record");
            graph.Permits("gamedataProbeDerived_Record", "gamedataProbeBase_Record");
            graph.Permits("gamedataProbeBase_Record", "gamedataProbeStranger_Record");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 10_000; i++)
        {
            graph.Permits("gamedataProbeBase_Record", "gamedataProbeDerived_Record");
            graph.Permits("gamedataProbeDerived_Record", "gamedataProbeBase_Record");
            graph.Permits("gamedataProbeBase_Record", "gamedataProbeStranger_Record");
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void APairNoSpellingCanAddressIsCountedAsUncheckedRatherThanChecked()
    {
        // There was nowhere to look, so nothing was looked at. Counted as
        // checked, this pair would report a check that could not have happened -
        // the same lie the validation manifest gives a state of its own to, and
        // that this reported nowhere at all.
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            Reference(new string('f', 60), "gamedataProbeThing_Record")));

        // A record whose own name is long enough that no field name fits beside
        // it inside an identifier.
        var source = new SyntheticReferenceSource()
            .WithRecord(TweakIdentifier.Of(new string('r', 250)), "gamedataProbeThing_Record");

        var check = Check(graph, source);

        Assert.Equal(0, check.TypedEdgesChecked);
        Assert.Equal(1, check.PairsNotAddressable);
        Assert.Equal(0, check.ReferencesFollowed);
    }

    [Fact]
    public void AnAddressablePairIsStillCountedAsChecked()
    {
        // The other arm, so that moving the count after the probing did not
        // quietly stop it counting.
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            Reference("owner", "gamedataProbeThing_Record")));

        var source = new SyntheticReferenceSource()
            .WithRecord(TweakIdentifier.Of("Probe.thing"), "gamedataProbeThing_Record");

        var check = Check(graph, source);

        Assert.Equal(1, check.TypedEdgesChecked);
        Assert.Equal(0, check.PairsNotAddressable);
    }

    [Fact]
    public void ARecordOfATypeTheSchemaLacksIsNamedRatherThanPassedOver()
    {
        // Such a record has no edges here, so it contributes to no count and
        // leaves a sweep looking as complete as one that had a schema for
        // everything it met.
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            Reference("owner", "gamedataProbeThing_Record")));

        var source = new SyntheticReferenceSource()
            .WithRecord(TweakIdentifier.Of("Probe.thing"), "gamedataProbeThing_Record")
            .WithRecord(TweakIdentifier.Of("Probe.alien"), "gamedataProbeUnknown_Record");

        var check = Check(graph, source);

        Assert.Equal(new[] { "gamedataProbeUnknown_Record" }, check.RecordTypesNotInSchema);
        Assert.Equal(1, check.TypedEdgesChecked);
    }

    [Fact]
    public void ASweepThatMetEveryTypeInItsSchemaNamesNone()
    {
        var graph = GraphOf(Type(
            "gamedataProbeThing_Record",
            null,
            Reference("owner", "gamedataProbeThing_Record")));

        var source = new SyntheticReferenceSource()
            .WithRecord(TweakIdentifier.Of("Probe.thing"), "gamedataProbeThing_Record");

        Assert.Empty(Check(graph, source).RecordTypesNotInSchema);
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
    public void AnEdgeIsFollowedUnderEverySpellingTheSchemaOffersForItsField()
    {
        // The failure this guards against does not look like a failure. A
        // schema derived from accessor shapes offers two spellings of a name;
        // the values are all stored under the one it did not lead with; and a
        // check probing only the leading spelling finds nothing, follows
        // nothing, and reports that nothing contradicted the graph. Every count
        // it prints is true and the run examined no reference at all.
        var check = Check(
            GraphOf(
                Type(
                    "gamedataProbeThing_Record",
                    null,
                    new RecordFieldShape("owner", "TweakDBID", ["Owner"], "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeOther_Record")
                .PointingFrom(1, "Owner", 2));

        Assert.Equal(1, check.ReferencesFollowed);
        Assert.Equal(1, check.ReferencesOfPermittedKind);
        Assert.Equal(1, check.ValuesRead);
    }

    [Fact]
    public void AWronglyTypedReferenceIsNamedUnderTheSpellingItWasFoundAt()
    {
        // Whoever reads the example goes looking for the value. Naming the
        // spelling the schema leads with, when the value sits at the other one,
        // sends them to an identifier that holds nothing.
        var check = Check(
            GraphOf(
                Type(
                    "gamedataProbeThing_Record",
                    null,
                    new RecordFieldShape("owner", "TweakDBID", ["Owner"], "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null),
                Type("gamedataProbeElse_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeElse_Record")
                .PointingFrom(1, "Owner", 2));

        Assert.Equal("Owner", Assert.Single(check.Examples).FieldName);
    }

    [Fact]
    public void AnEdgeIsNotFollowedUnderASpellingThatIsAnotherFieldsName()
    {
        // The exclusion, seen from the reference side. One field is really
        // called Owner and holds a reference of its own; another guesses that
        // its name might be spelled that way. Probing the guess would take the
        // first field's values as evidence about the second - and here that
        // would double every reference the type carries.
        var check = Check(
            GraphOf(
                Type(
                    "gamedataProbeThing_Record",
                    null,
                    new RecordFieldShape("owner", "TweakDBID", ["Owner"], "gamedataProbeOther_Record"),
                    new RecordFieldShape("Owner", "TweakDBID", [], "gamedataProbeOther_Record")),
                Type("gamedataProbeOther_Record", null)),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeOther_Record")
                .PointingFrom(1, "Owner", 2));

        Assert.Equal(1, check.ReferencesFollowed);
    }

    [Fact]
    public void AKindDerivingThroughAnAncestorThatIsNotARecordTypeStillDerives()
    {
        // The chain a record type's ancestry is allowed to take: through a
        // class that is carried so the chain resolves and is not itself a
        // record type. Walking only the record types stops there, and stopping
        // reads the same as reaching the top - so a reference naming a kind
        // that really does derive from the permitted one would be reported as
        // naming an unrelated kind, on the game's own data, with the graph
        // being right about it.
        var check = Check(
            GraphOf(
                Type("gamedataProbeThing_Record", null, Reference("owner", "gamedataProbeBase_Record")),
                Type("gamedataProbeBase_Record", null),
                new RecordTypeShape("ProbeCarrier", "gamedataProbeBase_Record", false, []),
                Type("gamedataProbeDerived_Record", "ProbeCarrier")),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeDerived_Record")
                .PointingFrom(1, "owner", 2));

        Assert.Equal(1, check.ReferencesOfPermittedKind);
        Assert.Equal(0, check.ReferencesOfOtherKind);
        Assert.True(check.NothingContradictsTheGraph);
    }

    [Fact]
    public void AnUnrelatedKindIsStillUnrelatedHoweverLongItsChainIs()
    {
        // The other arm: widening the walk must not make everything permitted.
        var check = Check(
            GraphOf(
                Type("gamedataProbeThing_Record", null, Reference("owner", "gamedataProbeBase_Record")),
                Type("gamedataProbeBase_Record", null),
                new RecordTypeShape("ProbeCarrier", null, false, []),
                Type("gamedataProbeElse_Record", "ProbeCarrier")),
            new SyntheticReferenceSource()
                .WithRecord(1, "gamedataProbeThing_Record")
                .WithRecord(2, "gamedataProbeElse_Record")
                .PointingFrom(1, "owner", 2));

        Assert.Equal(1, check.ReferencesOfOtherKind);
        Assert.False(check.NothingContradictsTheGraph);
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
