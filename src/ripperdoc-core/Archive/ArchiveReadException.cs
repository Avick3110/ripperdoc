namespace Ripperdoc.Core.Archive;

/// <summary>
/// A read of a mod directory's archives could not be completed.
/// </summary>
/// <remarks>
/// Carries the <see cref="Kind" /> so that a caller can act on the failure
/// without parsing the sentence, and so that the sentence itself is derived
/// from the kind rather than written once per site.
/// </remarks>
public sealed class ArchiveReadException : Exception
{
    internal ArchiveReadException(ArchiveFailureKind kind, string message, Exception? inner)
        : base(message, inner) =>
        Kind = kind;

    /// <summary>Which kind of failure this is.</summary>
    public ArchiveFailureKind Kind { get; }
}
