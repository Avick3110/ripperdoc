namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// The rules read out of one home, under the name of that home.
/// </summary>
/// <param name="Home">Where the rules were read from.</param>
/// <param name="Rules">The rules, as read.</param>
/// <remarks>
/// Ordering intent has more than one home and they do not hold the same rules.
/// A verdict computed over one of them is a verdict about that one, so the home
/// travels with the rules rather than being flattened away at the point they
/// are merged.
/// </remarks>
public sealed record OrderingRuleSet(string Home, IReadOnlyList<OrderingRule> Rules);
