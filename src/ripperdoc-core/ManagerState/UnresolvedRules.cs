namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// How many rules of one declared kind named something the reader could not
/// resolve to a mod.
/// </summary>
/// <param name="DeclaredKind">The kind as the rule spells it.</param>
/// <param name="Count">How many of them there were.</param>
/// <remarks>
/// A rule side names a file or a hint, and the graph needs a mod. A side that
/// resolves to none is reported here rather than given an invented node: two
/// spellings of one mod under two invented names are two nodes, and a cycle
/// running between them is a cycle nothing would see.
/// </remarks>
public readonly record struct UnresolvedRules(string DeclaredKind, int Count);
