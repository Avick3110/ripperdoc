using System.Globalization;
using Ripperdoc.Core.Archive;
using Ripperdoc.Naming;
using Xunit;
using Xunit.Abstractions;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Enumeration over a real install's archive lane.
/// </summary>
/// <remarks>
/// <para>
/// Tier (ii): this reads a real mod directory, which no runner has and which is
/// other people's mod content that this project does not carry. The gate runs
/// it when the environment names a directory and announces it as skipped, by
/// name, when nothing does. Run outside the gate with nothing named, it fails
/// rather than passing quietly.
/// </para>
/// <para>
/// What is asserted is what holds of any archive lane. A real lane changes
/// whenever its owner installs a mod, so counts taken from one install would
/// turn an ordinary install into a red run - the numbers are reported instead,
/// and the invariants are what fails.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.InstalledModArchives)]
public class InstalledModArchivesTests
{
    private readonly ITestOutputHelper _output;

    public InstalledModArchivesTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The environment variable naming the mod directory, derived from the
    /// brand rather than spelled out, so a rebrand does not leave a stale name
    /// here.
    /// </summary>
    public static string VariableName => Branding.Name.ToUpperInvariant() + "_MOD_ARCHIVES_PATH";

    private static string ModDirectory
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

    [Fact]
    public void EveryArchiveIsEitherReadOrReportedUnreadableWithAReason()
    {
        var inventory = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(ModDirectory);

        _output.WriteLine(Report(inventory));

        Assert.NotEmpty(inventory.Archives);
        Assert.All(inventory.Archives, archive =>
            Assert.True(
                archive.WasRead || !string.IsNullOrWhiteSpace(archive.UnreadableReason),
                $"'{archive.FileName}' was neither read nor given a reason it could not be"));
    }

    [Fact]
    public void EveryEntryOfEveryArchiveReadIsReportableWithoutANameBeingRequired()
    {
        // The whole point of the naming posture. A real lane carries entries no
        // source can name, and every one of them still has to come back with
        // something a caller can print and a later stage can key on.
        var inventory = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(ModDirectory);

        var entries = inventory.AllEntries.ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Display)));

        Assert.Equal(
            inventory.DistinctEntryCount,
            inventory.DistinctNamedCount + inventory.DistinctHashOnlyCount);
    }

    [Fact]
    public void TheDictionaryNamesMoreWithoutChangingWhatIsThere()
    {
        // The two postures disagree about how many entries have names and agree
        // about how many entries exist. If installing the dictionary ever
        // changed the second number, naming would be deciding what the
        // inventory contains, which it must never do.
        var directory = ModDirectory;

        var withoutDictionary = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(directory);
        var archiveOnlyNamed = withoutDictionary.DistinctNamedCount;
        var entryCount = withoutDictionary.DistinctEntryCount;
        var archiveCount = withoutDictionary.ArchiveCount;

        var withDictionary = new ArchiveInventoryReader(new DictionaryResourceNames()).Read(directory);

        _output.WriteLine(Report(withoutDictionary));
        _output.WriteLine(Report(withDictionary));

        Assert.Equal(archiveCount, withDictionary.ArchiveCount);
        Assert.Equal(entryCount, withDictionary.DistinctEntryCount);
        Assert.True(
            withDictionary.DistinctNamedCount >= archiveOnlyNamed,
            $"the dictionary posture named {withDictionary.DistinctNamedCount} of {entryCount} where the "
            + $"archive-only posture named {archiveOnlyNamed}; installing a naming source must never "
            + "reduce coverage");
    }

    [Fact]
    public void TheProvenanceSaysWhichPostureProducedTheInventory()
    {
        var directory = ModDirectory;

        var withoutDictionary = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(directory);
        var withDictionary = new ArchiveInventoryReader(new DictionaryResourceNames()).Read(directory);

        Assert.NotEqual(withoutDictionary.Provenance.NameSource, withDictionary.Provenance.NameSource);
        Assert.Contains("WolvenKit.Common", withDictionary.Provenance.NameSource, StringComparison.Ordinal);
    }

    private static string Report(ArchiveInventory inventory)
    {
        var named = inventory.DistinctNamedCount;
        var distinct = inventory.DistinctEntryCount;
        var share = distinct == 0
            ? "n/a"
            : (100.0 * named / distinct).ToString("F1", CultureInfo.InvariantCulture) + " %";

        return $"""
                naming posture ....... {inventory.Provenance.NameSource}
                pinned library ....... {inventory.Provenance.ResourceLibraryVersion}
                archives ............. {inventory.ArchiveCount} (unreadable {inventory.UnreadableCount})
                entries .............. {inventory.AllEntries.Count()}
                distinct resources ... {distinct}
                  named .............. {named} ({share})
                  hash-only .......... {inventory.DistinctHashOnlyCount}
                nested archives ...... {inventory.NestedArchivePaths.Count} (precedence unmeasured)
                """;
    }
}
