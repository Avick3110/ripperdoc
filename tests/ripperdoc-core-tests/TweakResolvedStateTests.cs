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
        Assert.Equal("beta", collision.Winner.OriginDirectory);
        Assert.Equal("250", collision.Winner.ValueText);
        Assert.Equal("alpha", Assert.Single(collision.Overridden).OriginDirectory);
        Assert.Equal("100", Assert.Single(collision.Overridden).ValueText);
        Assert.Equal(TweakDecisionRule.LastWriterInReadOrder, collision.Rule);
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
        Assert.Equal(2, Assert.Single(collision.Overridden).Line);
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
        Assert.Equal("alpha", contribution.OriginDirectory);
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

        Assert.Equal("gamma", collision.Winner.OriginDirectory);
        Assert.Equal(TweakDecisionRule.WrittenBeatsInherited, collision.Rule);

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

        Assert.Equal("aaa", collision.Winner.OriginDirectory);
        Assert.Equal(TweakFileGroup.First, Assert.Single(collision.Overridden).File.Group);
        Assert.Equal(TweakDecisionRule.GroupBeforeReadOrder, collision.Rule);
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
}
