namespace Ripperdoc.Core.Script;

/// <summary>
/// One annotation found in one source, at the rank that source holds.
/// </summary>
/// <param name="Kind">Which of the two contending annotations it is.</param>
/// <param name="Method">The method it targets.</param>
/// <param name="Source">The source it was read from.</param>
/// <param name="Line">The one-based line the annotation sits on.</param>
/// <param name="CallsWrappedMethod">
/// Whether the body beneath it calls the wrapped method. Meaningful for a wrap
/// and always false for a replacement, which has nothing beneath it to call.
/// </param>
public sealed record ScriptAnnotation(
    ScriptAnnotationKind Kind,
    MethodIdentity Method,
    ScriptSource Source,
    int Line,
    bool CallsWrappedMethod)
{
    /// <summary>
    /// Whether this is a wrap that never calls the method it wraps.
    /// </summary>
    /// <remarks>
    /// The compiler says nothing about this: a wrap whose body has no such call
    /// compiles with no error and no warning, measured. At run time the wraps it
    /// encloses never execute, so every mod inside it silently does nothing -
    /// and which mods those are depends on a chain position this engine does not
    /// claim to know.
    /// </remarks>
    public bool IsWrapThatDropsTheChain =>
        Kind == ScriptAnnotationKind.WrapMethod && !CallsWrappedMethod;
}
