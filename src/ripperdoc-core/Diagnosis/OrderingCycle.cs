namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// A cycle in the ordering rules, as the path that closes it.
/// </summary>
/// <param name="Path">
/// The mods the cycle runs through, in order, with the first repeated as the
/// last so the closure is in the value rather than in a convention the reader
/// has to know.
/// </param>
/// <remarks>
/// A path rather than a flag, because a flag is not actionable: the two rules a
/// user has to change are the edges of the path, and nothing downstream can
/// recover them from a boolean.
/// </remarks>
public sealed record OrderingCycle(IReadOnlyList<string> Path);
