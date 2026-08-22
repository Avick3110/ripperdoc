using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

// The framework moves a value written to a record the shipped data was copied
// from onto those copies. This engine does not follow that movement, so what it
// owes a reader is an honest account of where the movement could happen - and
// silence where it could not. These check that account, in both directions.
public class TweakIndirectMovementTests
{
    private const string Source = "Probe.stock";
    private const string Property = "price";

    private static ulong Record(string name) => TweakIdentifier.Of(name);

    // Descendants are keyed by identifier because that is all the framework's
    // metadata carries.
    private static TweakInheritanceMap MapOf(string source, params string[] descendants) =>
        TweakInheritanceMap.Of(
            new Dictionary<ulong, IReadOnlyList<ulong>>
            {
                [Record(source)] = descendants.Select(Record).ToArray(),
            },
            "a map built for this check");

    private sealed class Values : ITweakValueSource
    {
        private readonly HashSet<ulong> _records = [];

        public string Description => "a database built for this check";

        public Values WithRecord(string name)
        {
            _records.Add(Record(name));

            return this;
        }

        public bool HoldsRecord(ulong identifier) => _records.Contains(identifier);
    }

    [Fact]
    public void AWriteOnARecordTheShippedDataWasCopiedFromIsNamedWithWhatSitsUnderIt()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var state = layer.Replay(
            MapOf(Source, "Probe.copy", "Probe.other"),
            new Values().WithRecord(Source));

        Assert.True(state.InheritanceWasExamined);

        var write = Assert.Single(state.WritesOnAShippedBaseRecord);
        Assert.Equal($"{Source}.{Property}", write.FlatName);
        Assert.Equal(Source, write.RecordName);
        Assert.Equal(Record(Source), write.RecordIdentifier);
        Assert.Equal(2, write.DescendantCount);
    }

    [Fact]
    public void ALayerWritingToNoSuchRecordStatesNoLimitAtAll()
    {
        // The common case, and the one the wave is demonstrated on. A limit
        // printed here would be a sentence about a risk that is not present,
        // and a reader who meets enough of those stops reading the ones that
        // are.
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", "Probe.invented.price: 250\n"));

        var state = layer.Replay(MapOf(Source, "Probe.copy"), new Values().WithRecord(Source));

        Assert.True(state.InheritanceWasExamined);
        Assert.Empty(state.WritesOnAShippedBaseRecord);
        Assert.Null(state.IndirectMovementLimit);
    }

    [Fact]
    public void TheLimitIsStatedWhereSomethingIsUnaccountedForAndNowhereElse()
    {
        // The two arms come from one layer and one map, differing only in
        // whether the record written to is one the database holds. Anything
        // that made the limit unconditional, or dropped it entirely, breaks one
        // arm or the other.
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));
        var map = MapOf(Source, "Probe.copy");

        var opens = layer.Replay(map, new Values().WithRecord(Source));
        Assert.NotEmpty(opens.WritesOnAShippedBaseRecord);
        Assert.NotNull(opens.IndirectMovementLimit);
        Assert.Contains("1 value", opens.IndirectMovementLimit, StringComparison.Ordinal);

        var doesNot = layer.Replay(map, new Values());
        Assert.Empty(doesNot.WritesOnAShippedBaseRecord);
        Assert.Null(doesNot.IndirectMovementLimit);
    }

    [Fact]
    public void ANameTheLayerInventedIsNotTreatedAsAShippedRecord()
    {
        // The metadata records identifiers and a layer is free to name a record
        // of its own. Asking the map alone would report a limit over a record
        // the shipped data does not have, and so has no copies of.
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var state = layer.Replay(MapOf(Source, "Probe.copy"), new Values());

        Assert.True(state.InheritanceWasExamined);
        Assert.Empty(state.WritesOnAShippedBaseRecord);
    }

    [Fact]
    public void AWriteOnAShippedRecordNothingWasCopiedFromIsNotCounted()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        // The database holds the record; the metadata records nothing cloned
        // from it, so a write to it reaches nothing else.
        var state = layer.Replay(MapOf("Probe.elsewhere", "Probe.copy"), new Values().WithRecord(Source));

        Assert.Empty(state.WritesOnAShippedBaseRecord);
        Assert.Null(state.IndirectMovementLimit);
    }

    [Fact]
    public void AWriteThatMutatesAValueCountsAsMuchAsOneThatReplacesIt()
    {
        // A mutation changes what the copies would be given just as a
        // replacement does. Counting only replacements would report a layer as
        // fully accounted for while a write in it reached records nobody named.
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\a.yaml", $"{Source}.list:\n  - !append Probe.other\n"));

        var state = layer.Replay(MapOf(Source, "Probe.copy"), new Values().WithRecord(Source));

        var write = Assert.Single(state.WritesOnAShippedBaseRecord);
        Assert.Equal($"{Source}.list", write.FlatName);
    }

    [Fact]
    public void WithoutBothInputsTheQuestionIsReportedAsUnaskedRatherThanAnsweredZero()
    {
        using var layer = SyntheticTweakLayer.Of(("alpha\\a.yaml", $"{Source}.{Property}: 250\n"));

        var withoutDatabase = layer.Replay(MapOf(Source, "Probe.copy"), values: null);
        Assert.False(withoutDatabase.InheritanceWasExamined);
        Assert.Empty(withoutDatabase.WritesOnAShippedBaseRecord);

        var withoutMap = layer.Replay(TweakInheritanceMap.None, new Values().WithRecord(Source));
        Assert.False(withoutMap.InheritanceWasExamined);
        Assert.Empty(withoutMap.WritesOnAShippedBaseRecord);

        // Neither found a write on such a record, and neither is entitled to
        // report that as an answer. An empty list plus a silent limit would say
        // the layer was fully accounted for when nothing looked.
        //
        // The whole sentence is asserted, not a phrase inside it. This one is
        // built from two parts and printed verbatim into the tier (ii) report,
        // and a check looking for a phrase passes just as well when both parts
        // supply it and the reader is told the question was not established
        // twice over.
        const string Unasked = "whether any value this layer writes sits on a record the shipped data was "
            + "copied from was not established - ";
        const string Consequence = "; a write to such a record moves the copies of it too";

        Assert.Equal(Unasked + "no database" + Consequence, withoutDatabase.IndirectMovementLimit);
        Assert.Equal(Unasked + "no inheritance metadata" + Consequence, withoutMap.IndirectMovementLimit);

        Assert.Equal("not established - no database", withoutDatabase.InheritanceDescription);
        Assert.Equal("not established - no inheritance metadata", withoutMap.InheritanceDescription);
    }
}
