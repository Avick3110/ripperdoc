namespace Ripperdoc.Core.Script;

/// <summary>
/// What one source was found to contain.
/// </summary>
public sealed class ScriptFileReading
{
    internal ScriptFileReading(
        ScriptSource source,
        IReadOnlyList<ScriptAnnotation> annotations,
        IReadOnlyList<int> annotationsWithNoDeclaration)
    {
        Source = source;
        Annotations = annotations;
        AnnotationsWithNoDeclaration = annotationsWithNoDeclaration;
    }

    /// <summary>The source this reading is of.</summary>
    public ScriptSource Source { get; }

    /// <summary>The contending annotations it carries, in the order they appear.</summary>
    public IReadOnlyList<ScriptAnnotation> Annotations { get; }

    /// <summary>
    /// Lines carrying an annotation this engine could not attach to a function
    /// declaration.
    /// </summary>
    /// <remarks>
    /// Reported rather than dropped. Such a line is either something the reader
    /// does not understand or a source that would not compile, and both are
    /// worth a reader's attention - whereas silently skipping them would let a
    /// real replacement go unseen and its contest be reported one carrier short.
    /// </remarks>
    public IReadOnlyList<int> AnnotationsWithNoDeclaration { get; }
}
