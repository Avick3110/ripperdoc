using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The real install's archive lane, and the one dictionary-less reading of it
/// this process can honestly take.
/// </summary>
/// <remarks>
/// Its own home because more than one tier (ii) class reads this lane, and the
/// clean reading is a per-process resource rather than a per-class one. A
/// dictionary loads into a resolver that is process-wide and cannot be
/// unloaded, so "before any dictionary" happens exactly once in a run. Two
/// classes each keeping their own would race for that one moment: whichever
/// ran second would find a dictionary already in force, and its checks would
/// then either compare a posture against itself or fail for a reason that is
/// about the ordering of the run rather than about the engine.
/// </remarks>
internal static class InstalledModArchivesFixture
{
    /// <summary>
    /// The environment variable naming the mod directory, derived from the
    /// brand rather than spelled out, so a rebrand does not leave a stale name
    /// here.
    /// </summary>
    internal static string VariableName => Branding.Name.ToUpperInvariant() + "_MOD_ARCHIVES_PATH";

    /// <summary>The mod directory these checks read.</summary>
    internal static string ModDirectory
    {
        get
        {
            var path = Environment.GetEnvironmentVariable(VariableName);

            return string.IsNullOrWhiteSpace(path)
                ? throw new InvalidOperationException(
                    $"These checks read a real install's archive lane, which no runner has. Set "
                    + $"{VariableName} to a mod directory to run them. The gate script announces them as "
                    + "skipped, by name, when it cannot run them - an absent input is never reported as "
                    + "a pass.")
                : path;
        }
    }

    /// <summary>
    /// The lane read with no dictionary in force, taken once and shared.
    /// </summary>
    /// <remarks>
    /// The fence inside fails rather than adapting. Were it to accept a
    /// contaminated reading, the counts either side of a posture comparison
    /// would simply match and the comparison would pass having compared
    /// nothing - a check that stops discriminating without saying so.
    /// </remarks>
    internal static ArchiveInventory DictionaryLessReading => CleanReading.Value;

    /// <summary>Reads the lane with the posture that adds no dependency.</summary>
    internal static ArchiveInventory ReadWithoutDictionary() =>
        new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(ModDirectory);

    private static readonly Lazy<ArchiveInventory> CleanReading = new(() =>
    {
        var inventory = ReadWithoutDictionary();

        Assert.False(
            inventory.Provenance.DictionaryLoaded,
            "a dictionary was already loaded in this process before the dictionary-less reading was "
            + "taken, so that reading is not a dictionary-less one. These checks fail rather than "
            + "reporting a comparison they did not make.");

        return inventory;
    });
}
