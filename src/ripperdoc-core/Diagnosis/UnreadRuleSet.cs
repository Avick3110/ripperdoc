namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// A home of ordering rules that was not read, and why.
/// </summary>
/// <param name="Home">The home that went unread.</param>
/// <param name="Reason">What stopped it being read.</param>
/// <remarks>
/// A graph is only as complete as its inputs, and the difference between "no
/// cycle" and "no cycle in what I could see" is the whole difference between a
/// verdict and a wrong answer. An unread home is named on the result rather
/// than left to be inferred from its absence.
/// </remarks>
public sealed record UnreadRuleSet(string Home, string Reason);
