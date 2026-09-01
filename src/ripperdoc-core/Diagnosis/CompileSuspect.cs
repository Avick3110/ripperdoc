namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// A mod a compile failure implicates, with what implicates it.
/// </summary>
/// <param name="ModId">The manager's identity for the mod.</param>
/// <param name="Errors">The errors reported in sources the record attributes to it.</param>
/// <remarks>
/// The errors travel with the suspect rather than being counted. A named mod
/// with no evidence attached is an accusation, and the sources this engine can
/// offer - the compiler's own line and the record entry that attributes it -
/// are both in the value.
/// </remarks>
public sealed record CompileSuspect(string ModId, IReadOnlyList<CompileError> Errors);
