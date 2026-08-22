using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class TweakResolvedStateTests
{
    [Fact]
    public void TwoModsWritingOneValueIsAContestTheLaterReadOneWins()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\prices.yaml", "Probe.item.price: 100\n"),
            ("beta\\prices.yaml", "Probe.item.price: 250\n"));

        var collision = Assert.Single(layer.Replay().Collisions);

        Assert.Equal("Probe.item.price", collision.FlatName);
        Assert.Equal("beta", collision.Winner.Origin);
        Assert.Equal("250", collision.Winner.ValueText);
        Assert.Equal("alpha", Assert.Single(collision.Overridden).Contribution.Origin);
        Assert.Equal("100", Assert.Single(collision.Overridden).Contribution.ValueText);
        Assert.Equal(TweakDecisionRule.LastWriterInReadOrder, Assert.Single(collision.Rules));
    }

    [Fact]
    public void TheContestNamesTheFileTheLineAndThePositionInTheReadOrder()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\prices.yaml", "# a comment\nProbe.item.price: 100\n"),
            ("beta\\prices.yaml", "Probe.item.price: 250\n"));

        var collision = Assert.Single(layer.Replay().Collisions);
        var explanation = collision.Explain();

        Assert.Equal(2, collision.Winner.File.ReadPosition);
        Assert.Equal(2, Assert.Single(collision.Overridden).Contribution.Line);
        Assert.Contains("alpha\\prices.yaml:2", explanation, StringComparison.Ordinal);
        Assert.Contains("beta\\prices.yaml:1", explanation, StringComparison.Ordinal);
        Assert.Contains("read 2 of the layer", explanation, StringComparison.Ordinal);
        Assert.Contains("last writer wins", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContestCarriesTheIdentifierTheDatabaseKeysTheValueBy()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\a.yaml", "Probe.item.price: 100\n"),
            ("beta\\b.yaml", "Probe.item.price: 250\n"));

        var collision = Assert.Single(layer.Replay().Collisions);

        Assert.Equal(TweakIdentifier.Of("Probe.item.price"), collision.Identifier);
    }

    [Fact]
    public void TwoFilesInOneModWritingOneValueIsNotReportedAsAContest()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\one.yaml", "Probe.item.price: 100\n"),
            ("alpha\\two.yaml", "Probe.item.price: 250\n"));

        var state = layer.Replay();

        Assert.Empty(state.Collisions);

        // Both writes are still carried; only the report leaves them out.
        var flat = Assert.Single(state.Flats, candidate => candidate.Name == "Probe.item.price");
        Assert.Equal(2, flat.Contributions.Count);
        Assert.Equal("250", flat.Winner!.ValueText);
    }

    [Fact]
    public void TwoModsAppendingToOneListIsCompositionRatherThanAContest()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\list.yaml", "Probe.registry.entries:\n  - !append Probe.one\n"),
            ("beta\\list.yaml", "Probe.registry.entries:\n  - !append Probe.two\n"));

        var state = layer.Replay();

        Assert.Empty(state.Collisions);
        Assert.Equal(2, Assert.Single(state.Flats).Contributions.Count);
    }

    [Fact]
    public void AReplacementContestsAMutationsListButTheMutationIsNotTheLoser()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\list.yaml", "Probe.registry.entries:\n  - !append Probe.one\n"),
            ("beta\\list.yaml", "Probe.registry.entries:\n  - Probe.two\n"));

        var state = layer.Replay();

        // Only one party replaces the value, so there is nothing it overrode.
        Assert.Empty(state.Collisions);
        Assert.Equal("[Probe.two]", Assert.Single(state.Flats).Winner!.ValueText);
    }

    [Fact]
    public void AValueSetOnARecordMovesIntoACloneThatDidNotNameIt()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\base.yaml", "Probe.stock:\n  $type: gamedataProbeWidget_Record\n  price: 100\n"),
            ("beta\\clone.yaml", "Probe.special:\n  $base: Probe.stock\n  label: special\n"));

        var state = layer.Replay();
        var inherited = Assert.Single(state.Flats, flat => flat.Name == "Probe.special.price");
        var contribution = Assert.Single(inherited.Contributions);

        Assert.Equal(TweakContributionRoute.InheritedFromBase, contribution.Route);
        Assert.Equal("100", contribution.ValueText);
        Assert.Equal("Probe.stock.price", contribution.Inheritance!.SourceFlatName);

        // Whose value it is, and who carried it, are different origins and both
        // are kept.
        Assert.Equal("alpha", contribution.Origin);
        Assert.Equal("beta\\clone.yaml", contribution.File.RelativePath);
    }

    [Fact]
    public void AValueTheCloneSetsItselfIsNotReplacedByTheOneItWouldHaveInherited()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\base.yaml", "Probe.stock:\n  $type: gamedataProbeWidget_Record\n  price: 100\n"),
            ("beta\\clone.yaml", "Probe.special:\n  $base: Probe.stock\n  price: 999\n"));

        var state = layer.Replay();
        var flat = Assert.Single(state.Flats, candidate => candidate.Name == "Probe.special.price");

        Assert.Equal("999", flat.Winner!.ValueText);
        Assert.Equal(TweakContributionRoute.Written, flat.Winner.Route);
        Assert.Single(flat.Contributions);
    }

    [Fact]
    public void AWriteBeatingAnInheritedValueIsExplainedByThatRuleAndNotByReadOrder()
    {
        using var layer = SyntheticTweakLayer.Of(
            // The clone is declared first and inherits alpha's price; gamma then
            // writes the same value by name from a file read later.
            ("alpha\\base.yaml", "Probe.stock:\n  $type: gamedataProbeWidget_Record\n  price: 100\n"),
            ("beta\\clone.yaml", "Probe.special:\n  $base: Probe.stock\n"),
            ("gamma\\override.yaml", "Probe.special.price: 555\n"));

        var state = layer.Replay();
        var collision = Assert.Single(state.Collisions, candidate => candidate.FlatName == "Probe.special.price");

        Assert.Equal("gamma", collision.Winner.Origin);
        Assert.Equal(TweakDecisionRule.WrittenBeatsInherited, Assert.Single(collision.Rules));

        var explanation = collision.Explain();
        Assert.Contains("Probe.stock.price", explanation, StringComparison.Ordinal);
        Assert.Contains("alpha\\base.yaml:3", explanation, StringComparison.Ordinal);
        Assert.Contains("beta\\clone.yaml", explanation, StringComparison.Ordinal);
        Assert.Contains("not replaced by one arriving through a clone", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AContestDecidedByTheGroupingSaysSoRatherThanBlamingReadOrder()
    {
        using var layer = SyntheticTweakLayer.Of(
            // zzz sorts last in the walk, and its file is promoted to the first
            // group - so it is read FIRST and loses to aaa, which the walk
            // reaches first and the grouping does not touch.
            ("zzz\\_early.yaml", "Probe.item.price: 100\n"),
            ("aaa\\ordinary.yaml", "Probe.item.price: 250\n"));

        var state = layer.Replay();
        var collision = Assert.Single(state.Collisions);

        Assert.Equal("aaa", collision.Winner.Origin);
        Assert.Equal(TweakFileGroup.First, Assert.Single(collision.Overridden).Contribution.File.Group);
        Assert.Equal(TweakDecisionRule.GroupBeforeReadOrder, Assert.Single(collision.Rules));
        Assert.Contains("first character", collision.Explain(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANameWithNoIdentifierIsRecordedWithItsReasonRatherThanResolved()
    {
        var tooLong = "Probe." + new string('n', TweakIdentifier.MaxNameLength) + ".price";
        var outsideRange = "Probe.café.price";

        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\names.yaml", $"{tooLong}: 1\n{outsideRange}: 2\nProbe.fine.price: 3\n"));

        var state = layer.Replay();

        Assert.Equal("Probe.fine.price", Assert.Single(state.Flats).Name);
        Assert.Equal(
            new[] { FlatAddressing.NameTooLong, FlatAddressing.NameOutsideRange },
            state.Unaddressable.Select(name => name.Addressing).OrderBy(addressing => addressing));
        Assert.All(state.Unaddressable, name => Assert.Single(name.Contributions));
    }

    [Fact]
    public void AFormatTheReplayDoesNotCoverIsCarriedIntoTheStateRatherThanLeftOut()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\thing.yaml", "Probe.item.price: 1\n"),
            ("beta\\thing.tweak", "Probe.item : ProbeWidget\n{\n  int price = 2;\n}\n"));

        var state = layer.Replay();

        // The framework applies both. The replay covers one, and the state says
        // which one it did not - a contest computed here is qualified by it.
        Assert.Single(state.Flats);
        Assert.Contains(
            state.Unhandled,
            unhandled => unhandled.Path == "beta\\thing.tweak");
    }

    [Fact]
    public void ReplayingDocumentsThatAreNotTheEnumeratedLayerFailsRatherThanReportingWrongPositions()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", "Probe.item.price: 1\n"));

        var enumerated = layer.Enumerate();

        var tooFew = Assert.Throws<ArgumentException>(
            () => TweakResolvedState.Replay(enumerated, [], TweakInheritanceMap.None, null));
        Assert.Contains("1 files and 0 documents", tooFew.Message, StringComparison.Ordinal);

        var wrongFile = Assert.Throws<ArgumentException>(
            () => TweakResolvedState.Replay(
                enumerated,
                [new TweakDocument("other\\b.yaml", [], true)],
                TweakInheritanceMap.None,
                null));
        Assert.Contains("other\\b.yaml", wrongFile.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyLayerResolvesToNothingRatherThanFailing()
    {
        using var layer = SyntheticTweakLayer.OfEmpty();

        var state = layer.Replay();

        Assert.Empty(state.Flats);
        Assert.Empty(state.Collisions);
        Assert.Empty(state.Unhandled);
    }

    [Fact]
    public void TwoLosersThatLostForDifferentReasonsAreEachToldTheirOwnReason()
    {
        using var layer = SyntheticTweakLayer.Of(
            // alpha is promoted into the first group and read before both others.
            // beta and gamma are in the same group, so beta loses on read order
            // alone - and a rename would win it, which is not true for alpha.
            ("alpha\\_early.yaml", "Probe.item.price: 100\n"),
            ("beta\\b.yaml", "Probe.item.price: 200\n"),
            ("gamma\\c.yaml", "Probe.item.price: 300\n"));

        var collision = Assert.Single(layer.Replay().Collisions);

        Assert.Equal("gamma", collision.Winner.Origin);
        Assert.Equal(3, collision.OriginCount);

        var byOrigin = collision.Overridden.ToDictionary(
            write => write.Contribution.Origin,
            write => write.Rule,
            StringComparer.Ordinal);

        Assert.Equal(TweakDecisionRule.GroupBeforeReadOrder, byOrigin["alpha"]);
        Assert.Equal(TweakDecisionRule.LastWriterInReadOrder, byOrigin["beta"]);

        var explanation = collision.Explain();
        Assert.Contains(TweakCollision.Describe(TweakDecisionRule.GroupBeforeReadOrder), explanation, StringComparison.Ordinal);
        Assert.Contains(TweakCollision.Describe(TweakDecisionRule.LastWriterInReadOrder), explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOriginCountCountsOriginsAndNotWrites()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\one.yaml", "Probe.item.price: 100\n"),
            ("alpha\\two.yaml", "Probe.item.price: 150\n"),
            ("beta\\z.yaml", "Probe.item.price: 250\n"));

        var collision = Assert.Single(layer.Replay().Collisions);

        // Three writes, two mods. A reader told "three origins" goes looking
        // for a mod that is not there.
        Assert.Equal(2, collision.Overridden.Count);
        Assert.Equal(2, collision.OriginCount);
        Assert.Contains("is set by 2 origins", collision.Explain(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordDeclaredTwiceKeepsTheFirstDeclarationsSource()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\one.yaml", "Probe.first:\n  $type: gamedataProbeWidget_Record\n  price: 111\n"),
            ("beta\\two.yaml", "Probe.second:\n  $type: gamedataProbeWidget_Record\n  price: 222\n"),
            ("gamma\\a.yaml", "Probe.copy:\n  $base: Probe.first\n"),
            ("gamma\\b.yaml", "Probe.copy:\n  $base: Probe.second\n"));

        var state = layer.Replay();

        // The framework keeps the first declaration it sees for a record, so a
        // later one naming a different source changes nothing.
        var copied = Assert.Single(state.Flats, flat => flat.Name == "Probe.copy.price");
        Assert.Equal("111", copied.Winner!.ValueText);
    }

    [Fact]
    public void OnlyARecordsOwnPropertiesComeAcrossWithAClone()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\base.yaml", "Probe.stock.detail.nested: 5\nProbe.stock.price: 10\n"),
            ("beta\\clone.yaml", "Probe.special:\n  $base: Probe.stock\n"));

        var state = layer.Replay();

        // A clone takes the record's own properties. Probe.stock.detail.nested
        // belongs to Probe.stock.detail, not to Probe.stock, and copying it
        // would invent a value on a record nothing declared.
        Assert.Contains(state.Flats, flat => flat.Name == "Probe.special.price");
        Assert.DoesNotContain(state.Flats, flat => flat.Name.StartsWith("Probe.special.detail", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, FlatAddressing.Addressable)]
    [InlineData(1, FlatAddressing.NameTooLong)]
    public void TheNameLengthBoundaryIsWhereTheIdentifierStopsBeingDefined(int over, FlatAddressing expected)
    {
        // The guard's whole content is this edge: a name of exactly the longest
        // length has an identifier, and one byte more has none.
        const string prefix = "Probe.";
        var name = prefix + new string('n', TweakIdentifier.MaxNameLength - prefix.Length + over);
        Assert.Equal(TweakIdentifier.MaxNameLength + over, name.Length);

        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{name}: 1\n"));

        var state = layer.Replay();

        if (expected == FlatAddressing.Addressable)
        {
            Assert.Equal(name, Assert.Single(state.Flats).Name);
            Assert.Empty(state.Unaddressable);
        }
        else
        {
            Assert.Empty(state.Flats);
            Assert.Equal(expected, Assert.Single(state.Unaddressable).Addressing);
        }
    }

    [Fact]
    public void TwoFilesAtTheLayerRootAreSeparateOriginsBecauseNothingSaysOtherwise()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("one.yaml", "Probe.item.price: 100\n"),
            ("two.yaml", "Probe.item.price: 250\n"));

        var collision = Assert.Single(layer.Replay().Collisions);

        // A file at the root has no directory to be grouped by, so each is its
        // own origin. That is right for the single-file mods that live there
        // and wrong for two files one mod dropped side by side - and the report
        // says which file, so a reader can see which case they have.
        Assert.Equal("two.yaml", collision.Winner.Origin);
        Assert.Equal("one.yaml", Assert.Single(collision.Overridden).Contribution.Origin);
    }

    [Fact]
    public void ACloneOfACloneTakesTheValueThatWonRatherThanOneThatLost()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("a_alpha\\gen1.yaml", "Probe.gen1:\n  $type: gamedataProbeWidget_Record\n  price: 100\n"),
            ("b_beta\\gen2.yaml", "Probe.gen2:\n  $base: Probe.gen1\n"),
            ("c_gamma\\write.yaml", "Probe.gen2.price: 555\n"),
            ("d_delta\\gen3.yaml", "Probe.gen3:\n  $base: Probe.gen2\n"));

        var state = layer.Replay();

        // gen2 inherits 100 and is then written to by name, so 555 wins there.
        // gen3 clones gen2, and what it copies is the value gen2 actually holds.
        Assert.Equal("555", Assert.Single(state.Flats, flat => flat.Name == "Probe.gen2.price").Winner!.ValueText);
        Assert.Equal("555", Assert.Single(state.Flats, flat => flat.Name == "Probe.gen3.price").Winner!.ValueText);
    }

    [Fact]
    public void ACloneDeclaredAheadOfItsOwnCloneOfABaseComesUpShortAndIsToldSo()
    {
        // gen3 clones gen2, and gen2 clones gen1 - but gen3 is declared first.
        // The framework builds clone values as it walks the records in
        // declaration order, so by the time gen3 is built gen2 has not inherited
        // anything yet and gen3 gets none of it. That is what the game does and
        // it is reproduced; what would be wrong is letting it happen quietly.
        using var layer = SyntheticTweakLayer.Of(
            ("a_alpha\\gen1.yaml", "Probe.gen1:\n  $type: gamedataProbeWidget_Record\n  price: 100\n"),
            ("b_beta\\gen3.yaml", "Probe.gen3:\n  $base: Probe.gen2\n"),
            ("c_gamma\\gen2.yaml", "Probe.gen2:\n  $base: Probe.gen1\n"));

        var state = layer.Replay();

        // gen2 inherits the price; gen3 does not, because it was built first.
        Assert.Contains(state.Flats, flat => flat.Name == "Probe.gen2.price");
        Assert.DoesNotContain(state.Flats, flat => flat.Name == "Probe.gen3.price");

        var note = Assert.Single(state.Unhandled);
        Assert.Equal("Probe.gen3", note.Path);
        Assert.Contains("declares later", note.Reason, StringComparison.Ordinal);
        Assert.Contains("Probe.gen2", note.Reason, StringComparison.Ordinal);
        Assert.Contains("Probe.gen1", note.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ACloneWhoseBaseComesLaterButIsNotItselfACloneLosesNothingAndIsNotFlagged()
    {
        // The other arm. A base's own writes are staged before any record is
        // built, so a clone declared ahead of a plain base still gets them -
        // there is nothing to warn about, and warning anyway would teach a
        // reader to ignore the warning that matters.
        using var layer = SyntheticTweakLayer.Of(
            ("a_alpha\\clone.yaml", "Probe.copy:\n  $base: Probe.stock\n"),
            ("b_beta\\base.yaml", "Probe.stock:\n  $type: gamedataProbeWidget_Record\n  price: 100\n"));

        var state = layer.Replay();

        Assert.Equal(
            "100",
            Assert.Single(state.Flats, flat => flat.Name == "Probe.copy.price").Winner!.ValueText);
        Assert.Empty(state.Unhandled);
    }

    [Fact]
    public void ACloneOfARecordWhoseValueIsOnlyMutatedGainsNoEmptyValue()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\base.yaml", "Probe.stock.entries:\n  - !append Probe.one\n"),
            ("beta\\clone.yaml", "Probe.special:\n  $base: Probe.stock\n  label: special\n"));

        var state = layer.Replay();

        // Nothing replaced the base's list, so nothing came across. A value with
        // no contributor is not a value the layer writes, and counting it would
        // inflate every total taken from this list.
        Assert.DoesNotContain(state.Flats, flat => flat.Name == "Probe.special.entries");
        Assert.All(state.Flats, flat => Assert.NotEmpty(flat.Contributions));
    }
}
