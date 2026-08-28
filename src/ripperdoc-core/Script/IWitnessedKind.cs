namespace Ripperdoc.Core.Script;

/// <summary>
/// A kind that says when it applies and carries a layer that brings it about.
/// </summary>
/// <remarks>
/// The pair a completeness check needs, and the reason it is an interface
/// rather than two members on one type: the check has to be able to run over a
/// set that is deliberately wrong, or it is a check nobody has seen fail.
/// </remarks>
internal interface IWitnessedKind
{
    /// <summary>A layer this kind arises from.</summary>
    ScriptLayerWitness Witness { get; }

    /// <summary>Whether this kind applies to <paramref name="contest" />.</summary>
    /// <param name="contest">The result being judged.</param>
    bool AppliesTo(MethodContest contest);
}
