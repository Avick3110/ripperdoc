using Ripperdoc.Core.Diagnosis;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// Where a manifest would be for every curated list the manager stages, and a
/// refusal naming each staged list whose id could not say where.
/// </summary>
/// <param name="Paths">The paths, one per staged list whose id is usable.</param>
/// <param name="Refused">The staged lists whose ids are not, named one by one.</param>
/// <remarks>
/// The outcome is per staged list rather than per collection. One unusable id
/// refused for the whole expression leaves every other list's manifest absent
/// from the graph and absent from the unread homes as well - a verdict that
/// then looks complete apart from one generic home, which is the failure the
/// naming exists to prevent.
/// </remarks>
public sealed record StagedManifests(
    IReadOnlyList<string> Paths,
    IReadOnlyList<UnreadRuleSet> Refused);
