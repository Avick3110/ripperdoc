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

    /// <summary>
    /// An archive that declares its own paths has its entries named, with no
    /// dictionary anywhere.
    /// </summary>
    /// <remarks>
    /// The behaviour the default posture is named for, and the reason its
    /// coverage is not zero. Asserted against a real container rather than
    /// against the posture's description, because a description is a sentence
    /// and this is a claim about what the reader returns.
    /// </remarks>
    [Fact]
    public void AnArchiveDeclaringItsOwnPathsHasItsEntriesNamed()
    {
        SyntheticArchive.Write(_directory, "rdp_named.archive", @"base\rdp\alpha.json", @"base\rdp\beta.json");

        var entries = Reader().Read(_directory).AllEntries.ToList();

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.IsNamed));
        Assert.Equal(
            [@"base\rdp\alpha.json", @"base\rdp\beta.json"],
            entries.Select(entry => entry.Name).OrderBy(name => name, StringComparer.Ordinal));

        // Display is the name when there is one - the same property that falls
        // back to the hash when there is not.
        Assert.All(entries, entry => Assert.Equal(entry.Name, entry.Display));
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

    /// <summary>
    /// Every shape of unreadable archive becomes a row, and never ends the
    /// enumeration.
    /// </summary>
    /// <remarks>
    /// The four shapes are the ordinary ways a real mod directory holds a file
    /// that is not a readable archive: something that was never one, a
    /// placeholder, an interrupted download, and a damaged header. The library
    /// fails differently on each - two by returning, two by throwing, and the
    /// throwing pair name causes of their own that have nothing to do with the
    /// real one. What this holds is that all four end the same way: a row, a
    /// reason, and every other archive's entries still there.
    /// </remarks>
    [Theory]
    [InlineData("never-an-archive")]
    [InlineData("empty")]
    [InlineData("truncated")]
    [InlineData("damaged-header")]
    public void AnArchiveThatCannotBeReadBecomesARowAndLosesNothingElse(string shape)
    {
        SyntheticArchive.Write(_directory, "rdp_good.archive", @"base\rdp\a.json");
        var good = File.ReadAllBytes(Path.Combine(_directory, "rdp_good.archive"));
        var broken = Path.Combine(_directory, "rdp_broken.archive");

        switch (shape)
        {
            case "never-an-archive":
                File.WriteAllText(broken, "not an archive at all");
                break;
            case "empty":
                File.WriteAllBytes(broken, []);
                break;
            case "truncated":
                File.WriteAllBytes(broken, good[..Math.Min(64, good.Length)]);
                break;
            case "damaged-header":
                var damaged = (byte[])good.Clone();
                damaged[0] ^= 0xFF;
                damaged[1] ^= 0xFF;
                damaged[2] ^= 0xFF;
                damaged[3] ^= 0xFF;
                File.WriteAllBytes(broken, damaged);
                break;
        }

        var inventory = Reader().Read(_directory);

        Assert.Equal(2, inventory.ArchiveCount);
        Assert.Equal(1, inventory.UnreadableCount);

        // The good archive is still there with its entry. This is the half that
        // one bad file used to take down with it.
        var kept = inventory.Archives.Single(archive => archive.FileName == "rdp_good.archive");
        Assert.True(kept.WasRead);
        Assert.Single(kept.Entries);

        var row = inventory.Archives.Single(archive => archive.FileName == "rdp_broken.archive");
        Assert.False(row.WasRead);
        Assert.Empty(row.Entries);
        Assert.Contains("could not read this archive's index", row.UnreadableReason!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reason given for an unreadable archive does not tell the reader to
    /// go and check the wrong thing.
    /// </summary>
    /// <remarks>
    /// A sentence that directs an action is measured rather than reworded. The
    /// library surfaces a malformed container as a denied path, so a reason
    /// that repeated the underlying exception as its explanation would send
    /// someone to inspect permissions and antivirus for a file that is merely
    /// truncated. The underlying text is still carried - it is evidence - but
    /// it must not be the sentence's claim.
    /// </remarks>
    [Fact]
    public void TheReasonForAnUnreadableArchiveDoesNotDiagnoseACauseItCannotKnow()
    {
        SyntheticArchive.Write(_directory, "rdp_good.archive", @"base\rdp\a.json");
        var good = File.ReadAllBytes(Path.Combine(_directory, "rdp_good.archive"));
        File.WriteAllBytes(Path.Combine(_directory, "rdp_broken.archive"), good[..Math.Min(64, good.Length)]);

        var reason = Reader().Read(_directory)
            .Archives.Single(archive => archive.FileName == "rdp_broken.archive")
            .UnreadableReason!;

        // What the sentence leads with is the fact, not the library's guess.
        Assert.StartsWith("the pinned library could not read this archive's index", reason, StringComparison.Ordinal);

        // And it says the underlying error is evidence, so the access-denied
        // text it carries is not read as the diagnosis.
        Assert.Contains("evidence rather than a diagnosis", reason, StringComparison.Ordinal);
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

        // The pairing, not the flag's value. Whether a dictionary is loaded
        // depends on what else has run in this process, and a check asserting
        // either value outright would pass or fail on the order the runner
        // happened to pick. What must hold in every order is that the recorded
        // flag is the observed one.
        Assert.Equal(LoadedNameDictionary.IsLoaded(), provenance.DictionaryLoaded);
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
