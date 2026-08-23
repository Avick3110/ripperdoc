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
    /// Checks that read type information generated from a game install.
    /// </summary>
    public const string RttiDump = "RttiDump";
}
