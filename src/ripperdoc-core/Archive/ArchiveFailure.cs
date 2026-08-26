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
    /// Which kind a failed read is.
    /// </summary>
    /// <param name="exception">The error the read raised.</param>
    /// <param name="kind">
    /// The kind this failure belongs to. What was being read is known by the
    /// caller and not by the exception, which names neither.
    /// </param>
    /// <param name="operation">What the caller was doing.</param>
    /// <remarks>
    /// A directory listing can fail for causes this engine cannot name from the
    /// exception type, so anything but a denial falls to
    /// <see cref="ArchiveFailureKind.Unclassified" /> rather than being sorted
    /// into a kind whose message would assert more than was observed. A file
    /// read is not open the same way: it is reached only after the file was
    /// seen to exist, so every way it can fail is the one state
    /// <paramref name="kind" /> names, and the exception travels as evidence
    /// rather than as a diagnosis.
    /// </remarks>
    internal static ArchiveFailureKind Classify(
        Exception exception, ArchiveFailureKind kind, ArchiveOperation operation) =>
        operation switch
        {
            ArchiveOperation.FileRead => kind,
            _ => exception is UnauthorizedAccessException ? kind : ArchiveFailureKind.Unclassified,
        };

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
            + "a file that is present but unreadable here can be truncated, still downloading, or not "
            + "an archive despite its name.",

        ArchiveFailureKind.NamingFailed =>
            $"The index of '{subject}' was read, and naming its entries failed - {Trimmed(evidence)}. "
            + "The container is not implicated: what failed is this engine's own name resolution, and "
            + "reporting it as an unreadable archive would send a reader to inspect the wrong file.",

        ArchiveFailureKind.InaccessibleModDirectory =>
            $"The mod directory '{subject}' could not be listed - {Trimmed(evidence)}. No archive was "
            + "enumerated: what is refused is that path itself, not anything beneath it.",

        ArchiveFailureKind.InaccessibleSubdirectory =>
            $"A directory under '{subject}' could not be listed - {Trimmed(evidence)}. The archives in "
            + "the mod directory itself are still reported; the archives under that directory are not.",

        ArchiveFailureKind.UnreadableModlist =>
            $"The list file at '{subject}' could not be read - {Trimmed(evidence)}. Its order is what "
            + "decides which archive wins, so no order is reported: reading the directory as one "
            + "without a list would order every archive by file name and name a winner for every "
            + "contest, and those winners would be wrong wherever this file disagreed.",

        ArchiveFailureKind.MismatchedLoadOrder =>
            $"The load order offered was computed over a different reading than the archives being "
            + $"resolved - {subject}. Resolve against the order built from this same reading: an order "
            + "from another one supplies ranks that were never measured for these archives, and where it "
            + "supplies one for every carrier the winner reported is wrong with nothing said.",

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
