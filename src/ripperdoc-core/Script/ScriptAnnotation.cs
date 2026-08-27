namespace Ripperdoc.Core.Script;

/// <summary>
/// One annotation found in one source, at the rank that source holds.
/// </summary>
/// <param name="Kind">Which of the two contending annotations it is.</param>
/// <param name="Method">The method it targets.</param>
/// <param name="Source">The source it was read from.</param>
/// <param name="Line">The one-based line the annotation sits on.</param>
/// <param name="WrappedCall">What could be read about its call to the wrapped method.</param>
/// <param name="IsGated">
/// Whether a conditional-compilation gate stands immediately above it.
/// </param>
public sealed record ScriptAnnotation(
    ScriptAnnotationKind Kind,
    MethodIdentity Method,
    ScriptSource Source,
    int Line,
    WrappedCallReading WrappedCall,
    bool IsGated)
{
    /// <summary>
    /// Whether this is a wrap read to hold no call to the method it wraps.
    /// </summary>
    /// <remarks>
    /// Such a wrap compiles with no error and no warning, measured. False when
    /// the body could not be read, which is a separate state.
    /// </remarks>
    public bool IsWrapThatDropsTheChain =>
        Kind == ScriptAnnotationKind.WrapMethod && WrappedCall == WrappedCallReading.DoesNotCall;

    /// <summary>
    /// Whether this engine failed to read the body beneath this annotation.
    /// </summary>
    public bool BodyCouldNotBeRead => WrappedCall == WrappedCallReading.BodyNotResolved;

    /// <summary>How to name this annotation to a reader.</summary>
    public string Display => $"{Source.Display}:{Line}";
}
