using Xunit;

namespace Ripperdoc.Core.Tests;

public class UnequalValuePairTests
{
    // Same value, so a candidate agrees with itself and with any copy of it.
    private static bool Same(ulong left, ulong right) => left == right;

    [Fact]
    public void APairIsFoundEvenWhereTheFirstCandidatesAgreeWithEachOther()
    {
        // Taking a fixed two off the front and asserting they differ asserts
        // something about which values the input happens to carry. Two flats of
        // one record - a name and a displayName - can hold the same string, and
        // then there is no pair among the two and the check reds over an input
        // that is perfectly legitimate.
        var search = UnequalValuePair.FirstIn([7UL, 7UL, 9UL], Same, limit: 64);

        Assert.True(search.Found);
        Assert.Equal(7UL, search.Left);
        Assert.Equal(9UL, search.Right);
        Assert.Equal(3, search.Examined);
    }

    [Fact]
    public void ACandidateListThatAgreesThroughoutIsReportedAsNoPairRatherThanADefault()
    {
        // The other arm. A default pair returned as though it were a finding is
        // how "the input carries no two differing values" becomes "the engine
        // said two values match when they do not".
        var search = UnequalValuePair.FirstIn([7UL, 7UL, 7UL], Same, limit: 64);

        Assert.False(search.Found);
        Assert.Equal(3, search.Examined);
    }

    [Fact]
    public void TheScanStopsAtItsLimitRatherThanWalkingEverythingItIsGiven()
    {
        // Every candidate is compared against the ones before it, so an
        // unbounded scan over a database of millions is not a check anybody
        // would run. The bound is what makes the search affordable, and it is
        // reported rather than assumed.
        var search = UnequalValuePair.FirstIn(Repeated(7UL), Same, limit: 4);

        Assert.False(search.Found);
        Assert.Equal(4, search.Examined);
    }

    private static IEnumerable<ulong> Repeated(ulong value)
    {
        while (true)
        {
            yield return value;
        }
    }
}
