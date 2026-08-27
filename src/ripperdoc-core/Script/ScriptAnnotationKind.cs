namespace Ripperdoc.Core.Script;

/// <summary>
/// The annotation kinds this engine resolves.
/// </summary>
/// <remarks>
/// Only the two that contend for a method body. redscript has others, and a
/// source carrying one of those is read for these two and is otherwise not
/// interpreted here.
/// </remarks>
public enum ScriptAnnotationKind
{
    /// <summary>
    /// Takes the method body outright. Exactly one survives per method, and it
    /// is the last in compile order.
    /// </summary>
    ReplaceMethod,

    /// <summary>
    /// Wraps whatever body survives. Every wrap on a method is kept.
    /// </summary>
    WrapMethod,
}
