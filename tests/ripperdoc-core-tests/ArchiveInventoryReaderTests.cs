using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

public sealed class ArchiveInventoryReaderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ripperdoc-archive-tests-" + Guid.NewGuid().ToString("N"));

    public ArchiveInventoryReaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a check over.
        }
    }

    private static ArchiveInventoryReader Reader() => new(new ArchiveOnlyResourceNames());

    [Fact]
    public void EveryEntryOfEveryArchiveIsReported()
    {
        SyntheticArchive.Write(_directory, "rdp_one.archive", @"base\rdp\a.json", @"base\rdp\b.json");
        SyntheticArchive.Write(_directory, "rdp_two.archive", @"base\rdp\c.json");

        var inventory = Reader().Read(_directory);

        Assert.Equal(2, inventory.ArchiveCount);
        Assert.Equal(3, inventory.AllEntries.Count());
        Assert.All(inventory.Archives, archive => Assert.True(archive.WasRead));
    }

    [Fact]
    public void ArchivesAreOrderedByFileNameSoTwoRunsAgree()
    {
        SyntheticArchive.Write(_directory, "rdp_zulu.archive", @"base\rdp\z.json");
        SyntheticArchive.Write(_directory, "rdp_alpha.archive", @"base\rdp\a.json");

        var first = Reader().Read(_directory);
        var second = Reader().Read(_directory);

        Assert.Equal(
            new[] { "rdp_alpha.archive", "rdp_zulu.archive" },
            first.Archives.Select(archive => archive.FileName));
        Assert.Equal(
            first.Archives.Select(archive => archive.FileName),
            second.Archives.Select(archive => archive.FileName));
    }

    [Fact]
    public void AnArchiveThatCannotBeReadIsReportedWithAReasonRatherThanSkipped()
    {
        SyntheticArchive.Write(_directory, "rdp_good.archive", @"base\rdp\a.json");
        File.WriteAllText(Path.Combine(_directory, "rdp_broken.archive"), "not an archive at all");

        var inventory = Reader().Read(_directory);

        Assert.Equal(2, inventory.ArchiveCount);
        Assert.Equal(1, inventory.UnreadableCount);

        var broken = inventory.Archives.Single(archive => archive.FileName == "rdp_broken.archive");
        Assert.False(broken.WasRead);
        Assert.False(string.IsNullOrWhiteSpace(broken.UnreadableReason));
        Assert.Empty(broken.Entries);
    }

    [Fact]
    public void ArchivesInSubdirectoriesAreReportedSeparatelyAndNotCountedAsLoaded()
    {
        // Whether the game loads an archive from a subdirectory is not measured
        // by this project. Folding it into the loaded set would assert that it
        // does; dropping it would hide a file that is really there.
        SyntheticArchive.Write(_directory, "rdp_top.archive", @"base\rdp\a.json");

        var nested = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nested);
        SyntheticArchive.Write(nested, "rdp_nested.archive", @"base\rdp\b.json");

        var inventory = Reader().Read(_directory);

        Assert.Equal(1, inventory.ArchiveCount);
        Assert.Equal("rdp_top.archive", Assert.Single(inventory.Archives).FileName);
        Assert.Equal(
            Path.Combine("nested", "rdp_nested.archive"),
            Assert.Single(inventory.NestedArchivePaths));
    }

    [Fact]
    public void ADirectoryThatDoesNotExistIsAnnouncedRatherThanReadAsAnEmptyInstall()
    {
        var missing = Path.Combine(_directory, "no-such-directory");

        var thrown = Assert.Throws<DirectoryNotFoundException>(() => Reader().Read(missing));

        Assert.Contains(missing, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameSourceThatCannotLoadStopsTheReadBeforeAnyArchiveIsEnumerated()
    {
        // The failure this ordering prevents: an inventory whose every entry is
        // reported by hash while its provenance claims dictionary coverage.
        SyntheticArchive.Write(_directory, "rdp_one.archive", @"base\rdp\a.json");

        var reader = new ArchiveInventoryReader(new FailingNameSource());

        Assert.Throws<ResourceNameSourceException>(() => reader.Read(_directory));
    }

    [Fact]
    public void ProvenanceCarriesTheNamingPostureAndTheLibraryVersion()
    {
        SyntheticArchive.Write(_directory, "rdp_one.archive", @"base\rdp\a.json");

        var provenance = Reader().Read(_directory).Provenance;

        Assert.Equal(_directory, provenance.ModDirectory);
        Assert.Equal(new ArchiveOnlyResourceNames().Description, provenance.NameSource);
        Assert.Matches(@"^\d+\.\d+\.\d+$", provenance.ResourceLibraryVersion);
    }

    [Fact]
    public void AResourceCarriedByTwoArchivesIsOneDistinctEntryAndTwoEntries()
    {
        // The shape every contest downstream is built on: the same resource in
        // two archives. Counting it once as a resource and twice as an entry is
        // what lets a later stage see that there is something to resolve.
        SyntheticArchive.Write(_directory, "rdp_one.archive", @"base\rdp\shared.json");
        SyntheticArchive.Write(_directory, "rdp_two.archive", @"base\rdp\shared.json");

        var inventory = Reader().Read(_directory);

        Assert.Equal(2, inventory.AllEntries.Count());
        Assert.Equal(1, inventory.DistinctEntryCount);
    }

    [Fact]
    public void AnInventoryOfEntriesWithoutNamesCountsThemRatherThanLosingThem()
    {
        // Built directly rather than from a written archive: an archive this
        // project authors carries its own paths, so every entry in one is
        // named. What a real install has, and what this asserts the counting
        // survives, is entries with no name available at all.
        var contents = ArchiveContents.Read(
            "rdp_nameless.archive",
            [
                new ArchiveEntry(1, Name: null, 10, 10),
                new ArchiveEntry(2, Name: null, 20, 20),
                new ArchiveEntry(3, @"base\rdp\named.json", 30, 30),
            ]);

        var inventory = new ArchiveInventory([contents], [], default);

        Assert.Equal(3, inventory.AllEntries.Count());
        Assert.Equal(3, inventory.DistinctEntryCount);
        Assert.Equal(1, inventory.DistinctNamedCount);
        Assert.Equal(2, inventory.DistinctHashOnlyCount);
        Assert.Equal(1, contents.NamedCount);
        Assert.Equal(2, contents.HashOnlyCount);
    }

    [Fact]
    public void AResourceNamedByOneArchiveAndNamelessInAnotherCountsAsNamedOnce()
    {
        var named = ArchiveContents.Read("rdp_a.archive", [new ArchiveEntry(7, @"base\rdp\x.json", 1, 1)]);
        var nameless = ArchiveContents.Read("rdp_b.archive", [new ArchiveEntry(7, Name: null, 1, 1)]);

        var inventory = new ArchiveInventory([named, nameless], [], default);

        Assert.Equal(2, inventory.AllEntries.Count());
        Assert.Equal(1, inventory.DistinctEntryCount);
        Assert.Equal(1, inventory.DistinctNamedCount);
        Assert.Equal(0, inventory.DistinctHashOnlyCount);
    }

    private sealed class FailingNameSource : IResourceNameSource
    {
        public string Description => "a source that cannot load";

        public void Prepare() => throw new ResourceNameSourceException("deliberately unavailable");
    }
}
