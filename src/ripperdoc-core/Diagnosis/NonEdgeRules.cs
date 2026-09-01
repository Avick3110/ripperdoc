namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// How many rules of one kind the graph read without turning into edges.
/// </summary>
/// <param name="Kind">The kind that produced no edge.</param>
/// <param name="Count">How many rules of it were read.</param>
/// <remarks>
/// The count is reported rather than the rules, because the point it serves is
/// whether the graph is quieter than its inputs. A non-zero count beside a
/// no-cycle verdict says the verdict was reached over fewer claims than the
/// manager holds, which a reader has to be able to see.
/// </remarks>
public readonly record struct NonEdgeRules(OrderingRuleKind Kind, int Count);
