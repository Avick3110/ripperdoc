namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// One error the script compiler reported, and where it reported it.
/// </summary>
/// <param name="Code">The compiler's own word for what went wrong.</param>
/// <param name="SourcePath">The source file the compiler named, as it named it.</param>
/// <param name="Line">The line in that file.</param>
/// <param name="Column">The column on that line.</param>
/// <remarks>
/// The path is carried exactly as the compiler wrote it. It is the only thing
/// in the line that can be joined to anything else, and rewriting it here would
/// put the join's correctness in a place a reader cannot see.
/// </remarks>
public readonly record struct CompileError(string Code, string SourcePath, int Line, int Column);
