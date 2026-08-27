namespace Ripperdoc.Core.Script;

/// <summary>
/// What one source was found to contain.
/// </summary>
public sealed class ScriptFileReading
{
    internal ScriptFileReading(
        ScriptSource source,
        IReadOnlyList<ScriptAnnotation> annotations,
        IReadOnlyList<int> annotationsNotResolvedToAMethod)
    {
        Source = source;
        Annotations = annotations;
        AnnotationsNotResolvedToAMethod = annotationsNotResolvedToAMethod;
    }

    /// <summary>The source this reading is of.</summary>
    public ScriptSource Source { get; }

    /// <summary>The contending annotations it carries, in the order they appear.</summary>
    public IReadOnlyList<ScriptAnnotation> Annotations { get; }

    /// <summary>
    /// Lines carrying an annotation this engine contends over and could not
    /// resolve to a method.
    /// </summary>
    /// <remarks>
    /// Two doors reach this list: no function declaration beneath the
    /// annotation, and an argument whose shape this engine does not model. What
    /// they have in common is the part that matters - the annotation names no
    /// method here, so which contest it belongs to is exactly what is unknown
    /// about it. Reported rather than dropped, because a line skipped this way
    /// is a carrier of somebody's contest and the contest would be reported one
    /// carrier short.
    /// </remarks>
    public IReadOnlyList<int> AnnotationsNotResolvedToAMethod { get; }
}
