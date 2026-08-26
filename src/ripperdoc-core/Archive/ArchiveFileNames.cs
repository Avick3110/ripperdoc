namespace Ripperdoc.Core.Archive;

/// <summary>
/// How an archive's file name is matched against a name written down
/// somewhere else.
/// </summary>
/// <remarks>
/// One home for the rule, because the list file and the directory are two
/// spellings of the same name and a disagreement between them decides which
/// archive wins.
/// <para>
/// The comparison ignores case because the game is a Windows title and its mod
/// directory is a Windows path, where <c>Foo.archive</c> and
/// <c>foo.archive</c> are the same file. A case-sensitive match would read a
/// list entry whose spelling differs only in case as naming nothing, and the
/// archive would then be ordered as unlisted - a wrong winner reached
/// silently, which is the failure this layer exists to remove.
/// </para>
/// </remarks>
internal static class ArchiveFileNames
{
    /// <summary>The comparison a name match uses.</summary>
    internal static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;
}
