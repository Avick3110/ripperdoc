namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// What one ordering rule claims about the two mods it names.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="Before" /> and <see cref="After" /> are precedence claims,
/// and only those two become edges in <see cref="OrderingGraph" />. A
/// <see cref="Requires" /> rule says a mod needs another present, which a
/// manager may or may not also treat as an order; no measurement here
/// establishes that it does, so turning one into an edge would put a cycle
/// verdict on an assumption. They are counted and reported instead.
/// </para>
/// <para>
/// A rule whose kind this set does not name is
/// <see cref="Unmodelled" /> rather than dropped, because a rule silently
/// discarded is a rule that cannot contradict anything - the graph would come
/// back a DAG for the reason that it was never given the edge.
/// </para>
/// </remarks>
public enum OrderingRuleKind
{
    /// <summary>The kind could not be matched to any this set names.</summary>
    Unmodelled = 0,

    /// <summary>The rule's source loads before the mod it references.</summary>
    Before,

    /// <summary>The rule's source loads after the mod it references.</summary>
    After,

    /// <summary>The rule's source needs the mod it references to be present.</summary>
    Requires,
}
