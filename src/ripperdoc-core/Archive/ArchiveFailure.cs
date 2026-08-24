namespace Ripperdoc.Core.Archive;

/// <summary>
/// The sentence each failure kind is entitled to say, and the classification
/// that picks one.
/// </summary>
/// <remarks>
/// One home, so that a kind's wording cannot drift between the place it is
/// thrown and the place it is recorded on a row.
/// </remarks>
internal static class ArchiveFailure
{
    /// <summary>
    /// How an underlying error is carried: its type and its own words, so that
    /// a message can present it without adopting it.
    /// </summary>
    internal static string Evidence(Exception exception) =>
        $"it raised {exception.GetType().Name}: {exception.Message}";

    /// <summary>
    /// Which kind an enumeration failure is.
    /// </summary>
    /// <remarks>
    /// Only one exception type is classified, because only one has a cause this
    /// engine can name from the type alone. Everything else falls to
    /// <see cref="ArchiveFailureKind.Unclassified" /> rather than being sorted
    /// into a kind whose message would then assert more than was observed.
    /// </remarks>
    internal static ArchiveFailureKind Classify(Exception exception) =>
        exception is UnauthorizedAccessException
            ? ArchiveFailureKind.InaccessibleSubdirectory
            : ArchiveFailureKind.Unclassified;

    /// <summary>
    /// Builds the exception for a failure that ends the read.
    /// </summary>
    internal static ArchiveReadException Failure(
        ArchiveFailureKind kind,
        string subject,
        Exception? inner) =>
        new(kind, Describe(kind, subject, inner is null ? null : Evidence(inner)), inner);

    /// <summary>
    /// The message for one kind, saying what that kind knows and no more.
    /// </summary>
    internal static string Describe(ArchiveFailureKind kind, string subject, string? evidence) => kind switch
    {
        ArchiveFailureKind.MalformedContainer =>
            $"the pinned library could not read this archive's index - {Trimmed(evidence)}. "
            + "The underlying error names a cause of its own, which is evidence rather than a diagnosis; "
            + "a file that is present but unreadable here is most often truncated, still downloading, or "
            + "not an archive despite its name.",

        ArchiveFailureKind.NamingFailed =>
            $"The index of '{subject}' was read, and naming its entries failed - {Trimmed(evidence)}. "
            + "The container is not implicated: what failed is this engine's own name resolution, and "
            + "reporting it as an unreadable archive would send a reader to inspect the wrong file.",

        ArchiveFailureKind.InaccessibleSubdirectory =>
            $"A directory under '{subject}' could not be listed - {Trimmed(evidence)}. The enumeration "
            + "stops here rather than returning a mod set that silently omits whatever is beneath it.",

        ArchiveFailureKind.NotADirectory =>
            $"There is a file at '{subject}', not a directory. The path resolves, so this is not a "
            + "missing install - it is a path that names the wrong kind of thing to enumerate archives "
            + "from.",

        _ =>
            $"'{subject}' could not be enumerated - {Trimmed(evidence)}. This engine has no "
            + "classification for that failure, so it states the underlying error and claims nothing "
            + "about the cause.",
    };

    private static string Trimmed(string? evidence) =>
        evidence is null ? "no underlying error was reported" : evidence.TrimEnd('.');
}
