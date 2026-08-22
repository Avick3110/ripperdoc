using System.Globalization;

namespace Ripperdoc.Core.Tweak;

/// <summary>
/// One value that more than one part of the layer wrote, with the rule that
/// decided it.
/// </summary>
/// <remarks>
/// <para>
/// This value, from this mod, because of this rule. The last of the three is
/// what makes the report worth reading: a user who is told which mod won still
/// cannot act on it, and a user who is told <em>why</em> can rename a file, move
/// a directory, or decide the winner was the one they wanted.
/// </para>
/// <para>
/// A contest needs more than one origin. Two files in one mod writing one value
/// is that mod's own business and settled by the same rule; reporting it would
/// bury the contests that cross a boundary somebody can do something about.
/// </para>
/// </remarks>
public sealed record TweakCollision(
    string FlatName,
    ulong Identifier,
    TweakContribution Winner,
    IReadOnlyList<OverriddenWrite> Overridden)
{
    /// <summary>
    /// How many origins set this value, the winner included.
    /// </summary>
    /// <remarks>
    /// Origins, not writes. Two files in one mod that both lose to a third are
    /// one loser as far as a reader is concerned, and counting them as two
    /// sends that reader looking for a mod which is not there.
    /// </remarks>
    public int OriginCount => Overridden
        .Select(write => write.Contribution.OriginDirectory)
        .Append(Winner.OriginDirectory)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    /// <summary>
    /// Every rule that decided some part of this contest, in a stable order.
    /// </summary>
    public IReadOnlyList<TweakDecisionRule> Rules => Overridden
        .Select(write => write.Rule)
        .Distinct()
        .OrderBy(rule => rule)
        .ToArray();

    /// <summary>
    /// The contest over a value, or null where there is none.
    /// </summary>
    /// <param name="flat">The resolved value.</param>
    /// <returns>The contest, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="flat"/> is null.</exception>
    public static TweakCollision? For(ResolvedFlat flat)
    {
        ArgumentNullException.ThrowIfNull(flat);

        var winner = flat.Winner;
        if (winner is null)
        {
            return null;
        }

        var overridden = flat.Overridden
            .Where(contribution => !string.Equals(
                contribution.OriginDirectory,
                winner.OriginDirectory,
                StringComparison.OrdinalIgnoreCase))
            .Select(contribution => new OverriddenWrite(contribution, RuleFor(winner, contribution)))
            .ToArray();

        if (overridden.Length == 0)
        {
            return null;
        }

        return new TweakCollision(flat.Name, flat.Identifier, winner, overridden);
    }

    /// <summary>
    /// The contest in words, with its provenance.
    /// </summary>
    /// <returns>The explanation, one line per party plus the rule.</returns>
    public string Explain()
    {
        var lines = new List<string>
        {
            $"{FlatName} (identifier 0x{Identifier.ToString("X", CultureInfo.InvariantCulture)}) "
            + $"is set by {OriginCount} origins.",
            $"  winner: {Describe(Winner)}",
        };

        // The rule goes with the loser it applies to, not with the contest. In
        // a contest of three, one may have lost to the grouping and another to
        // read order inside its own group - and those two authors have to do
        // different things about it.
        foreach (var write in Overridden)
        {
            lines.Add($"  overridden: {Describe(write.Contribution)}");
            lines.Add($"      rule: {Describe(write.Rule)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// What a rule means, in a sentence fit to appear in a report.
    /// </summary>
    /// <param name="rule">The rule to describe.</param>
    /// <returns>The description, with no leading capital and no full stop.</returns>
    /// <remarks>
    /// The rule and its wording live together, so that a report naming a winner
    /// names the reason the decision actually turned on rather than the one that
    /// usually applies.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rule"/> is not a rule.
    /// </exception>
    public static string Describe(TweakDecisionRule rule) => rule switch
    {
        TweakDecisionRule.LastWriterInReadOrder =>
            "apply order is read order and the last writer wins, so the file read latest set the value "
            + "and the others were replaced without any warning",
        TweakDecisionRule.WrittenBeatsInherited =>
            "a value a file sets by name is not replaced by one arriving through a clone, whichever was "
            + "read first",
        TweakDecisionRule.GroupBeforeReadOrder =>
            "the files are read in groups taken from the first character of each file's own name, and the "
            + "winner is in a later group than this one, so read order never came into it",
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "There is no such rule."),
    };

    // Decided per loser, because that is the grain at which it is actionable.
    // In a contest of three, one write can lose to the grouping while another
    // loses to read order inside its own group, and those two authors have to
    // do different things about it - so a single rule for the whole contest
    // tells one of them to change something that would not have helped.
    //
    // Asked most-specific first: every one of these is true of a contest the
    // later ones also describe, and naming the general rule where a specific
    // one decided it sends the reader to change the wrong thing. The test is
    // that the value arrived without being named rather than that it arrived a
    // particular way, so a further indirect route would fall under this rule
    // without changing it.
    private static TweakDecisionRule RuleFor(TweakContribution winner, TweakContribution overridden)
    {
        if (winner.Route == TweakContributionRoute.Written
            && overridden.Route != TweakContributionRoute.Written)
        {
            return TweakDecisionRule.WrittenBeatsInherited;
        }

        return overridden.File.Group != winner.File.Group
            ? TweakDecisionRule.GroupBeforeReadOrder
            : TweakDecisionRule.LastWriterInReadOrder;
    }

    private static string Describe(TweakContribution contribution)
    {
        var where = $"{contribution.OriginDirectory} - {contribution.File.RelativePath}:{contribution.Line}, "
            + $"read {Position(contribution.File.ReadPosition)} of the layer";

        if (contribution.Inheritance is not { } inheritance)
        {
            return $"{where}, as {contribution.ValueText}";
        }

        var source = inheritance.Source;

        // Both halves are named. The clone is how the value arrived and the
        // write is whose value it is, and a reader who is given only one of them
        // goes and edits the wrong file.
        return $"{source.OriginDirectory} - set at {source.File.RelativePath}:{source.Line} as "
            + $"{source.ValueText}, on {inheritance.SourceFlatName}, and carried here by the clone "
            + $"declared at {contribution.File.RelativePath}:{contribution.Line} "
            + $"(read {Position(contribution.File.ReadPosition)} of the layer)";
    }

    private static string Position(int position) => position.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The rule that decided a contest.
/// </summary>
/// <remarks>
/// Told apart because each one points somewhere different. A contest decided by
/// read order is changed by renaming; one decided by grouping is changed by a
/// single character at the front of a name; one decided by a value being written
/// rather than inherited is not changed by ordering at all.
/// </remarks>
public enum TweakDecisionRule
{
    /// <summary>The last file to write the value in the read order won.</summary>
    LastWriterInReadOrder,

    /// <summary>
    /// A value written by name beat one that arrived through a clone.
    /// </summary>
    WrittenBeatsInherited,

    /// <summary>
    /// The winner's file is read in a later group than the loser's, so the
    /// grouping decided it before read order could.
    /// </summary>
    GroupBeforeReadOrder,
}

/// <summary>
/// One write that did not decide a value, and the rule that beat it.
/// </summary>
/// <param name="Contribution">The write.</param>
/// <param name="Rule">Why this one lost, which is not necessarily why another did.</param>
public sealed record OverriddenWrite(TweakContribution Contribution, TweakDecisionRule Rule);
