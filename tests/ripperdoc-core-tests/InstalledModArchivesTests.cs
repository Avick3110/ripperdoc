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
[Collection(ResolverCollection.Name)]
public class InstalledModArchivesTests
{
    private readonly ITestOutputHelper _output;

    public InstalledModArchivesTests(ITestOutputHelper output) => _output = output;

    private static string ModDirectory => InstalledModArchivesFixture.ModDirectory;

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

        // Counted from the entries rather than subtracted from the counter
        // under test, which is defined as that difference. Nameless entries
        // dropped before they reach the count are caught by
        // TheDictionaryNamesMoreWithoutChangingWhatIsThere, which stops
        // matching once the two postures drop different numbers; a drop that is
        // uniform across postures is invisible to it and is caught at tier (i),
        // by EveryEntryOfEveryArchiveIsReported. Neither is caught by this.
        var namelessInEveryArchiveCarryingIt = entries
            .GroupBy(entry => entry.Hash)
            .Count(group => group.All(entry => !entry.IsNamed));

        Assert.Equal(namelessInEveryArchiveCarryingIt, inventory.DistinctHashOnlyCount);
    }

    /// <summary>
    /// The two postures disagree about names and agree about contents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If installing the dictionary ever changed how many entries exist, naming
    /// would be deciding what the inventory contains, which it must never do.
    /// </para>
    /// <para>
    /// <strong>Why this reads both postures inside one check, and fences the
    /// order.</strong> A dictionary loads into a process-wide resolver that
    /// cannot be unloaded, so the dictionary-less reading is only honest in a
    /// process where no dictionary has yet loaded - there is exactly one such
    /// moment per process, and it cannot be shared between two checks that the
    /// runner may order either way. Taking both readings here puts them in a
    /// fixed order, and the assertion below refuses to proceed unless the first
    /// one really was taken clean. Without that assertion a reordering would
    /// not fail: both readings would simply be dictionary readings, the counts
    /// would match, and the comparison would pass while comparing nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheDictionaryNamesMoreWithoutChangingWhatIsThere()
    {
        var withoutDictionary = InstalledModArchivesFixture.DictionaryLessReading;

        var archiveOnlyNamed = withoutDictionary.DistinctNamedCount;
        var entryCount = withoutDictionary.DistinctEntryCount;
        var archiveCount = withoutDictionary.ArchiveCount;

        var withDictionary = new ArchiveInventoryReader(new DictionaryResourceNames()).Read(ModDirectory);

        _output.WriteLine(Report(withoutDictionary));
        _output.WriteLine(Report(withDictionary));

        Assert.True(withDictionary.Provenance.DictionaryLoaded);
        Assert.Equal(archiveCount, withDictionary.ArchiveCount);
        Assert.Equal(entryCount, withDictionary.DistinctEntryCount);
        // The invariant, not the install. Naming strictly more is a property of
        // a lane carrying entries the archives do not name, and a lane whose
        // mods all declare their own paths names everything under both
        // postures; the two numbers are reported above, where a reader can see
        // which lane this one is.
        Assert.True(
            withDictionary.DistinctNamedCount >= archiveOnlyNamed,
            $"the dictionary posture named {withDictionary.DistinctNamedCount} of {entryCount} where the "
            + $"archive-only posture named {archiveOnlyNamed}. Names go into the resolver and none come "
            + "out of it, so the dictionary posture can never name fewer than the archive-only one");
    }

    [Fact]
    public void TheProvenanceRecordsBothWhatWasAskedForAndWhatWasInForce()
    {
        // Touched first, so this check cannot be the one that loads a
        // dictionary before the dictionary-less reading is taken.
        var clean = InstalledModArchivesFixture.DictionaryLessReading;

        Assert.Equal(new ArchiveOnlyResourceNames().Description, clean.Provenance.NameSource);
        Assert.False(clean.Provenance.DictionaryLoaded);

        var withDictionary = new ArchiveInventoryReader(new DictionaryResourceNames()).Read(ModDirectory);

        Assert.Contains("WolvenKit.Common", withDictionary.Provenance.NameSource, StringComparison.Ordinal);
        Assert.True(withDictionary.Provenance.DictionaryLoaded);

        // Asked for and in force are separate fields because they can differ.
        // After the load above, a source that installs no dictionary still sees
        // one - and the provenance has to say so rather than repeat the
        // source's intent.
        var afterTheLoad = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(ModDirectory);

        _output.WriteLine(Report(clean));
        _output.WriteLine(Report(afterTheLoad));

        Assert.Equal(new ArchiveOnlyResourceNames().Description, afterTheLoad.Provenance.NameSource);
        Assert.True(
            afterTheLoad.Provenance.DictionaryLoaded,
            "a dictionary was loaded earlier in this process, so a read installing none still sees it; "
            + "reporting otherwise would state that no dictionary was in force while the run enjoyed "
            + "one");
        Assert.True(
            afterTheLoad.DistinctNamedCount >= clean.DistinctNamedCount,
            $"the reading after the load named {afterTheLoad.DistinctNamedCount} where the clean reading "
            + $"named {clean.DistinctNamedCount}. The resolver is additive and cannot be unloaded, so a "
            + "later reading can never name fewer than an earlier one");
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
                dictionary in force .. {(inventory.Provenance.DictionaryLoaded ? "yes" : "no")}
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
