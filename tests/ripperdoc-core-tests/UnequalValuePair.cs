namespace Ripperdoc.Core.Tests;

/// <summary>
/// The search for two values that do not match each other.
/// </summary>
/// <remarks>
/// Its own type, taking the comparison as a function, because the check it
/// serves runs only where the game's own database is present. A search written
/// inline there would be exercised against one file on one machine and never
/// against a stated input, so the case it exists to handle - candidates that
/// happen to agree - would never be reached deliberately.
/// </remarks>
internal static class UnequalValuePair
{
    /// <summary>
    /// The first two candidates that do not match.
    /// </summary>
    /// <param name="candidates">The values to consider, in order.</param>
    /// <param name="valuesMatch">Whether two values are the same value.</param>
    /// <param name="limit">
    /// How many candidates to examine before giving up. A bound rather than the
    /// whole database, because every candidate is compared against the ones
    /// before it and an unbounded scan over a file of millions is not a check
    /// anybody would run.
    /// </param>
    /// <returns>
    /// The pair, or a result saying none was found among those examined.
    /// </returns>
    internal static PairSearch FirstIn(
        IEnumerable<ulong> candidates,
        Func<ulong, ulong, bool> valuesMatch,
        int limit)
    {
        var seen = new List<ulong>();

        foreach (var candidate in candidates)
        {
            foreach (var earlier in seen)
            {
                if (!valuesMatch(earlier, candidate))
                {
                    return new PairSearch(Found: true, earlier, candidate, seen.Count + 1);
                }
            }

            seen.Add(candidate);

            if (seen.Count >= limit)
            {
                break;
            }
        }

        return new PairSearch(Found: false, 0UL, 0UL, seen.Count);
    }
}

/// <summary>What a search found, and how much of the input it looked at.</summary>
/// <param name="Found">Whether a pair that does not match was found.</param>
/// <param name="Left">One of the pair, where one was found.</param>
/// <param name="Right">The other, where one was found.</param>
/// <param name="Examined">How many candidates were looked at.</param>
internal readonly record struct PairSearch(bool Found, ulong Left, ulong Right, int Examined);
