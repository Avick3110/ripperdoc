namespace Ripperdoc.Core.Archive;

/// <summary>
/// What this engine was doing when a read failed.
/// </summary>
/// <remarks>
/// A failure means different things depending on what was attempted, and the
/// exception alone does not carry that. Listing a directory can fail for causes
/// this engine cannot attribute, so an unrecognised one is reported as
/// unclassified rather than as a cause it has not established. Reading a file
/// this engine has already seen exist is narrower: however it fails, the file
/// is there and its contents did not come back, which is one state with one
/// name.
/// </remarks>
internal enum ArchiveOperation
{
    /// <summary>Listing the entries of a directory.</summary>
    DirectoryListing,

    /// <summary>Reading the contents of one file.</summary>
    FileRead,
}
