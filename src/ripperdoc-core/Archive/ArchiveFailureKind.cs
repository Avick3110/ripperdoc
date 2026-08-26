namespace Ripperdoc.Core.Archive;

/// <summary>
/// What kind of failure the archive layer met.
/// </summary>
/// <remarks>
/// A kind exists here only where the failure has been observed and its message
/// can say something the kind actually knows. The alternative - one message on
/// one channel covering every cause - has to guess which cause it is looking
/// at, and the pinned library gives it nothing to guess from: a merely
/// truncated file arrives as an access-denied exception.
/// <para>
/// <see cref="Unclassified" /> is the arm for everything else. It carries the
/// underlying error and asserts no cause at all, which is the only honest thing
/// to say about a failure this engine has no classification for.
/// </para>
/// </remarks>
public enum ArchiveFailureKind
{
    /// <summary>
    /// The failure has no classification here, and the message claims none.
    /// </summary>
    Unclassified,

    /// <summary>
    /// The pinned library could not read an archive's index.
    /// </summary>
    MalformedContainer,

    /// <summary>
    /// An archive's index was read, and this engine's own naming of its entries
    /// failed.
    /// </summary>
    NamingFailed,

    /// <summary>
    /// A directory under the mod directory could not be listed.
    /// </summary>
    InaccessibleSubdirectory,

    /// <summary>
    /// The path given resolves to a file rather than to a directory.
    /// </summary>
    NotADirectory,

    /// <summary>
    /// The mod directory itself could not be listed.
    /// </summary>
    InaccessibleModDirectory,

    /// <summary>
    /// The mod directory has a list file and it could not be read.
    /// </summary>
    /// <remarks>
    /// Its own kind because of what the alternative costs. A list that cannot
    /// be read is not a directory without one, and treating it as one orders
    /// every archive by file name - a complete, confident set of winners that
    /// is wrong wherever the list disagreed.
    /// </remarks>
    UnreadableModlist,
}
