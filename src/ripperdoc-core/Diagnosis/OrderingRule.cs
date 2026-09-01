namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// One pairwise ordering rule, as the manager or a curated list states it.
/// </summary>
/// <param name="Source">The mod the rule is about.</param>
/// <param name="Reference">The mod it is stated against.</param>
/// <param name="Kind">What the rule claims.</param>
/// <remarks>
/// Both ends are the identity the reader resolved them to, not the spelling the
/// rule used. A rule side names a file - by expression, by hash, by logical
/// name - and two sides naming one mod through two of those spellings are one
/// node or the graph is wrong in the direction that hides a cycle.
/// </remarks>
public readonly record struct OrderingRule(string Source, string Reference, OrderingRuleKind Kind);
