namespace Ripperdoc.Core.Tests;

/// <summary>
/// Which tier a check belongs to, as a trait the gate can filter on.
/// </summary>
/// <remarks>
/// Its own file because it belongs to no single tier. Living inside the checks
/// for one of them made the next tier's constant land in a file named for a
/// different domain, which is how a file starts collecting things that only
/// happened to be added near each other.
/// </remarks>
public static class TierTrait
{
    /// <summary>The trait's name.</summary>
    public const string Name = "Tier";

    /// <summary>Checks that read a shipped tweak database.</summary>
    public const string ShippedDatabase = "ShippedDatabase";

    /// <summary>Checks that read a real install's tweak lane.</summary>
    public const string InstalledTweakLayer = "InstalledTweakLayer";

    /// <summary>
    /// Checks that read a real install's archive lane - the mod directory and
    /// the archives in it.
    /// </summary>
    /// <remarks>
    /// Its own tier rather than a second use of the tweak-layer one: a machine
    /// can have mods that ship archives and no tweak files, or the reverse, and
    /// a tier whose name covered both would be half true whenever only one of
    /// them was there.
    /// </remarks>
    public const string InstalledModArchives = "InstalledModArchives";

    /// <summary>
    /// Checks that read type information generated from a game install.
    /// </summary>
    public const string RttiDump = "RttiDump";

    /// <summary>
    /// Checks that need a directory the running user is refused.
    /// </summary>
    /// <remarks>
    /// Its own tier because the refusal is built with <c>icacls</c> and exists
    /// only on Windows, and the gate is the one place that can announce a tier
    /// it cannot run.
    /// </remarks>
    public const string DeniedDirectory = "DeniedDirectory";
}
