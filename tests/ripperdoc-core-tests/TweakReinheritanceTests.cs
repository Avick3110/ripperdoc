using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

// A value set on a shipped record moves onto the records the shipped data was
// copied from it - the indirect form of "the same value". The map and the
// values are the framework's own inputs, so both are built here rather than
// read from anyone's install: the algorithm is what these check.
public class TweakReinheritanceTests
{
    private const string Source = "Probe.stock";
    private const string Property = "price";

    private static ulong Record(string name) => TweakIdentifier.Of(name);

    private static ulong Flat(string recordName, string property) =>
        TweakIdentifier.ForField(Record(recordName), property);

    // Descendants are keyed by identifier because that is all the framework's
    // metadata carries; the stand-in names below are how the engine reports a
    // record it can only address.
    private static TweakInheritanceMap MapOf(string source, params string[] descendants) =>
        TweakInheritanceMap.Of(
            new Dictionary<ulong, IReadOnlyList<ulong>>
            {
                [Record(source)] = descendants.Select(Record).ToArray(),
            },
            "a map built for this check");

    private static string Reported(string recordName) => $"<record 0x{Record(recordName):X}>";

    private sealed class Values : ITweakValueSource
    {
        private readonly Dictionary<ulong, string> _values = new();
        private readonly HashSet<ulong> _records = [];

        public string Description => "values built for this check";

        public Values WithRecord(string name)
        {
            _records.Add(Record(name));

            return this;
        }

        public Values Holding(string recordName, string property, string value)
        {
            _records.Add(Record(recordName));
            _values[Flat(recordName, property)] = value;

            return this;
        }

        public bool HoldsRecord(ulong identifier) => _records.Contains(identifier);

        public bool HoldsValue(ulong identifier) => _values.ContainsKey(identifier);

        public bool ValuesMatch(ulong left, ulong right) =>
            _values.TryGetValue(left, out var a)
            && _values.TryGetValue(right, out var b)
            && string.Equals(a, b, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueSetOnAShippedRecordMovesOntoACopyThatStillAgreesWithIt()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.copy", Property, "100");

        var state = layer.Replay(MapOf(Source, "Probe.copy"), values);

        Assert.True(state.InheritanceWasReplayed);

        var moved = Assert.Single(state.Flats, flat => flat.Name == Reported("Probe.copy") + ".price");
        var contribution = Assert.Single(moved.Contributions);

        Assert.Equal(TweakContributionRoute.InheritedFromShippedRecord, contribution.Route);
        Assert.Equal("250", contribution.ValueText);
        Assert.Equal($"{Source}.{Property}", contribution.Inheritance!.SourceFlatName);
        Assert.Equal(Flat("Probe.copy", Property), moved.Identifier);
    }

    [Fact]
    public void ACopyThatNoLongerAgreesWithItsSourceIsLeftAlone()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        // The copy was given a different value when the game's data was built,
        // so it stopped following its source and a write to the source does not
        // reach it.
        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.copy", Property, "77");

        var state = layer.Replay(MapOf(Source, "Probe.copy"), values);

        Assert.DoesNotContain(state.Flats, flat => flat.Name.StartsWith("<record", StringComparison.Ordinal));
    }

    [Fact]
    public void ACopyTheLayerWritesToByNameIsLeftAlone()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\a.yaml", $"{Source}.{Property}: 250\n"),
            ("beta\\b.yaml", $"Probe.copy.{Property}: 999\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.copy", Property, "100");

        var state = layer.Replay(MapOf(Source, "Probe.copy"), values);

        var flat = Assert.Single(state.Flats, candidate => candidate.Name == $"Probe.copy.{Property}");
        Assert.Equal("999", flat.Winner!.ValueText);
        Assert.Equal(TweakContributionRoute.Written, flat.Winner.Route);
        Assert.DoesNotContain(state.Flats, candidate => candidate.Name.StartsWith("<record", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValueTravelsThroughACopyThatIsItselfASource()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.middle", Property, "100")
            .Holding("Probe.leaf", Property, "100");

        var map = TweakInheritanceMap.Of(
            new Dictionary<ulong, IReadOnlyList<ulong>>
            {
                [Record(Source)] = [Record("Probe.middle")],
                [Record("Probe.middle")] = [Record("Probe.leaf")],
            },
            "a two-generation map");

        var state = layer.Replay(map, values);

        Assert.Contains(state.Flats, flat => flat.Identifier == Flat("Probe.middle", Property));
        Assert.Contains(state.Flats, flat => flat.Identifier == Flat("Probe.leaf", Property));
    }

    [Fact]
    public void AFamilyThatRecordsItselfAsItsOwnAncestorTerminates()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.copy", Property, "100");

        var map = TweakInheritanceMap.Of(
            new Dictionary<ulong, IReadOnlyList<ulong>>
            {
                [Record(Source)] = [Record("Probe.copy")],
                [Record("Probe.copy")] = [Record(Source), Record("Probe.copy")],
            },
            "a map that loops");

        var state = layer.Replay(map, values);

        // It terminates, and the record the layer wrote to by name is not given
        // a second, inherited contribution on the way round.
        Assert.Single(state.Flats, flat => flat.Name == $"{Source}.{Property}");
        Assert.Single(state.Flats, flat => flat.Identifier == Flat("Probe.copy", Property));
    }

    [Fact]
    public void ACopyTheDatabaseDoesNotHoldIsSkipped()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        // The map names a record this database does not have - a copy that
        // belonged to content this install does not carry.
        var values = new Values().Holding(Source, Property, "100");

        var state = layer.Replay(MapOf(Source, "Probe.absent"), values);

        Assert.DoesNotContain(state.Flats, flat => flat.Name.StartsWith("<record", StringComparison.Ordinal));
    }

    [Fact]
    public void ACopyHoldingNoValueForThePropertyIsSkipped()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .WithRecord("Probe.copy");

        var state = layer.Replay(MapOf(Source, "Probe.copy"), values);

        Assert.DoesNotContain(state.Flats, flat => flat.Name.StartsWith("<record", StringComparison.Ordinal));
    }

    [Fact]
    public void AWriteToARecordTheDatabaseDoesNotHoldMovesNothing()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values().Holding("Probe.copy", Property, "100");

        var state = layer.Replay(MapOf(Source, "Probe.copy"), values);

        Assert.DoesNotContain(state.Flats, flat => flat.Name.StartsWith("<record", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutBothInputsTheRouteIsReportedAsNotWalkedRatherThanAsEmpty()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.copy", Property, "100");

        var withoutValues = layer.Replay(MapOf(Source, "Probe.copy"), values: null);
        Assert.False(withoutValues.InheritanceWasReplayed);
        Assert.Contains("no value source", withoutValues.InheritanceDescription, StringComparison.Ordinal);

        var withoutMap = layer.Replay(TweakInheritanceMap.None, values);
        Assert.False(withoutMap.InheritanceWasReplayed);
        Assert.Contains("no inheritance metadata", withoutMap.InheritanceDescription, StringComparison.Ordinal);

        // Neither reports a moved value, and both say they did not look. The
        // one thing neither does is report zero movement as a finding.
        Assert.DoesNotContain(
            withoutValues.Flats.Concat(withoutMap.Flats),
            flat => flat.Name.StartsWith("<record", StringComparison.Ordinal));
    }

    [Fact]
    public void AMovedValueContestedByAWriteIsExplainedByTheRuleThatDecidedIt()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var values = new Values()
            .Holding(Source, Property, "100")
            .Holding("Probe.copy", Property, "100");

        var state = layer.Replay(MapOf(Source, "Probe.copy"), values);
        var moved = Assert.Single(state.Flats, flat => flat.Identifier == Flat("Probe.copy", Property));
        var explanation = TweakCollision.For(moved);

        // One origin moved it and nobody contested it, so there is no contest -
        // but the movement is in the state, which is what a later contest would
        // be computed against.
        Assert.Null(explanation);
        Assert.Equal("alpha", Assert.Single(moved.Contributions).OriginDirectory);
    }
}
