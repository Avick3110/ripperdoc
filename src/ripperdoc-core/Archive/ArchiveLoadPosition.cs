namespace Ripperdoc.Core.Archive;

/// <summary>
/// Where one archive sits in the order the game loads them.
/// </summary>
/// <param name="FileName">The archive's file name, without its directory.</param>
/// <param name="Rank">
/// Its place in the load order. A lower rank loads first, and the first-loaded
/// archive wins every resource it carries.
/// </param>
/// <param name="IsListed">Whether the mod directory's list file names it.</param>
/// <remarks>
/// <strong>Two archives sharing a rank is the measurement's own residue, not a
/// tie this type invented.</strong> Equal ranks say that neither archive is
/// known to load before the other, and every consumer of a rank has to carry
/// that through rather than break the tie: the published law measured that
/// unlisted archives load after every listed one, and did not measure their
/// order among themselves.
/// </remarks>
public readonly record struct ArchiveLoadPosition(string FileName, int Rank, bool IsListed);
