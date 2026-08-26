namespace Ripperdoc.Core.Archive;

/// <summary>
/// How an archive the list file does not name fared in the contests it is
/// part of.
/// </summary>
/// <param name="FileName">The unlisted archive's file name.</param>
/// <param name="ContestsCarried">How many contested resources it carries.</param>
/// <param name="ContestsLostToListedArchives">
/// How many of those a listed archive wins.
/// </param>
/// <param name="ContestsUndetermined">
/// How many of those have no winner this project can name, because their
/// leading carriers share a rank.
/// </param>
/// <remarks>
/// This exists because of what the measurement showed about a partial list:
/// being named on it outranks any file name, so adding a mod to an install
/// that has one puts it below every mod already there. The mod loads, it
/// works, and it loses every conflict it has - and nothing in the install says
/// so. Reporting the winners correctly would still leave that state invisible,
/// so it is computed and named here.
/// <para>
/// Renaming does not fix it. Promoting an archive by prefixing its file name
/// does nothing at all while the archives beating it are listed and it is not.
/// </para>
/// </remarks>
public readonly record struct ArchiveDemotion(
    string FileName,
    int ContestsCarried,
    int ContestsLostToListedArchives,
    int ContestsUndetermined)
{
    /// <summary>
    /// Whether every contest this archive is part of goes to a listed archive.
    /// </summary>
    /// <remarks>
    /// The hazard in full: the archive contributes nothing to any resource it
    /// shares, and its file name has no bearing on that.
    /// </remarks>
    public bool LosesEveryContestToTheList =>
        ContestsCarried > 0 && ContestsLostToListedArchives == ContestsCarried;
}
