namespace Ripperdoc.Core.Archive;

/// <summary>
/// One archive in the mod directory, and every resource it carries.
/// </summary>
/// <remarks>
/// Either the archive was read and <see cref="Entries" /> is its full entry
/// set, or it was not and <see cref="UnreadableReason" /> says so.
/// </remarks>
public sealed class ArchiveContents
{
    private ArchiveContents(
        string fileName,
        IReadOnlyList<ArchiveEntry> entries,
        string? unreadableReason,
        ArchiveFailureKind? failureKind)
    {
        FileName = fileName;
        Entries = entries;
        UnreadableReason = unreadableReason;
        FailureKind = failureKind;
    }

    /// <summary>The archive's file name, without its directory.</summary>
    public string FileName { get; }

    /// <summary>
    /// Every resource the archive carries, named or not. Empty when the archive
    /// could not be read - check <see cref="WasRead" /> before reading a
    /// meaning into an empty list.
    /// </summary>
    public IReadOnlyList<ArchiveEntry> Entries { get; }

    /// <summary>
    /// Why the archive could not be read, or <see langword="null" /> when it
    /// was read.
    /// </summary>
    public string? UnreadableReason { get; }

    /// <summary>
    /// Which kind of failure kept the archive from being read, or
    /// <see langword="null" /> when it was read.
    /// </summary>
    /// <remarks>
    /// Carried beside the sentence so that a caller acts on the kind rather
    /// than on the wording.
    /// </remarks>
    public ArchiveFailureKind? FailureKind { get; }

    /// <summary>Whether the archive's entries were actually read.</summary>
    public bool WasRead => UnreadableReason is null;

    /// <summary>How many entries a naming source could name.</summary>
    public int NamedCount => Entries.Count(entry => entry.IsNamed);

    /// <summary>
    /// How many entries are reported by hash because no name was available.
    /// </summary>
    public int HashOnlyCount => Entries.Count - NamedCount;

    /// <summary>Records an archive that was read, with its entries.</summary>
    public static ArchiveContents Read(string fileName, IReadOnlyList<ArchiveEntry> entries) =>
        new(fileName, entries, unreadableReason: null, failureKind: null);

    /// <summary>
    /// Records an archive that was found but could not be read, under the kind
    /// of failure that stopped it.
    /// </summary>
    /// <remarks>
    /// The sentence is derived from the kind rather than supplied, so a row
    /// cannot be given a reason that says more than its kind knows.
    /// </remarks>
    internal static ArchiveContents Unreadable(
        string fileName,
        ArchiveFailureKind kind,
        string? evidence) =>
        new(fileName, [], ArchiveFailure.Describe(kind, fileName, evidence), kind);
}
