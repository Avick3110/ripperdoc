using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The published precedence law, reproduced over archives this project wrote.
/// </summary>
/// <remarks>
/// Every row of the finding's two decision tables is a case below, named for
/// the boot it comes from. The archives are synthetic and the contested
/// resource is an invented path, because precedence turns on which archive
/// loads first and not on which resource is contested - so no game-derived
/// byte is needed to reproduce a row.
/// </remarks>
[Collection(ResolverCollection.Name)]
public sealed class ArchiveLoadOrderTests : IDisposable
{
    private const string ContestedPath = @"base\rdp\contested.json";

    private const string ArchiveA = "rdp_a.archive";
    private const string ArchiveB = "rdp_b.archive";
    private const string Detector = "rdp_c.archive";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ripperdoc-order-tests-" + Guid.NewGuid().ToString("N"));

    public ArchiveLoadOrderTests() => Directory.CreateDirectory(_directory);

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

    /// <summary>
    /// Both decision tables, one case per boot.
    /// </summary>
    /// <remarks>
    /// The presence detector is asserted on every row rather than only on the
    /// rows of the pass it came from. Its column is what separated "loaded and
    /// lost" from "never loaded" in the measurement, and shadowing is
    /// whole-file - so an archive that loses a contest must still contribute
    /// the resources nothing contests, in every branch of the law.
    /// </remarks>
    [Theory]
    [InlineData("pass one, boot 1 (control) - no list file", null, ArchiveA)]
    [InlineData("pass one, boot 2a - the list gives B then A", ArchiveB + "|" + ArchiveA, ArchiveB)]
    [InlineData("pass one, boot 2b - the list gives A then B", ArchiveA + "|" + ArchiveB, ArchiveA)]
    [InlineData("pass one, boot 3 (revert) - the list is deleted", null, ArchiveA)]
    [InlineData("pass two, boot X - B listed, A and the detector unlisted", ArchiveB, ArchiveB)]
    [InlineData("pass two, boot Y - A listed, B and the detector unlisted", ArchiveA, ArchiveA)]
    public void TheWinnerIsTheOneTheLawNames(string boot, string? listedNames, string expectedWinner)
    {
        WriteTheThreeArchives();
        WriteModlist(listedNames);

        var contested = Resolve();

        var contest = Assert.Single(contested.Contests);
        Assert.Equal(expectedWinner, contest.Winner);
        Assert.True(contest.HasDeterminedWinner, boot + " left the winner undetermined");
        Assert.Equal(ContestedPath, contest.Name);

        // The presence detector: unlisted in the pass-two rows, and carrying a
        // resource nothing contests.
        Assert.Contains(Detector, contested.Order.Positions.Select(position => position.FileName));
        Assert.DoesNotContain(Detector, contested.Contests.SelectMany(entry => entry.Carriers.Select(carrier => carrier.FileName)));
    }

    /// <summary>
    /// The pass-two revert row, in the only form a resolver can take it.
    /// </summary>
    /// <remarks>
    /// That row records the game reading a vanilla value once the contesting
    /// archives were removed. What the resolver can be held to is the same
    /// directory shape: with them gone there is no contest, and the archive
    /// that never contested anything still carries what it carries.
    /// </remarks>
    [Fact]
    public void WithTheContestingArchivesGoneThereIsNoContestLeftToResolve()
    {
        SyntheticArchive.Write(_directory, Detector, @"base\rdp\detector.json");

        var contested = Resolve();

        Assert.Empty(contested.Contests);
        Assert.Equal(1, contested.DistinctResourceCount);
        Assert.Equal(1, contested.ResourcesUncontestedAtThisBasis);
    }

    [Fact]
    public void WithNoListFileTheOrderIsByFileNameAndEveryPlaceIsKnown()
    {
        SyntheticArchive.Write(_directory, "rdp_zulu.archive", @"base\rdp\z.json");
        SyntheticArchive.Write(_directory, "rdp_alpha.archive", @"base\rdp\a.json");

        var order = Order();

        Assert.False(order.Modlist.IsPresent);
        Assert.Equal(
            new[] { "rdp_alpha.archive", "rdp_zulu.archive" },
            order.Positions.Select(position => position.FileName));
        Assert.Equal(new[] { 0, 1 }, order.Positions.Select(position => position.Rank));
        Assert.All(order.Positions, position => Assert.False(position.IsListed));
        Assert.True(order.IsFullyOrdered);
    }

    /// <summary>
    /// Being listed outranks any file name.
    /// </summary>
    /// <remarks>
    /// The consequence the finding calls out as the opposite of what the
    /// renaming instinct expects, held here as an ordering rather than as a
    /// winner: the archive whose name sorts last loads first because the list
    /// names it.
    /// </remarks>
    [Fact]
    public void AListedArchiveOutranksAnUnlistedOneWhateverTheirNames()
    {
        SyntheticArchive.Write(_directory, "rdp_aaa.archive", @"base\rdp\a.json");
        SyntheticArchive.Write(_directory, "rdp_zzz.archive", @"base\rdp\z.json");
        WriteModlist("rdp_zzz.archive");

        var order = Order();

        var listed = Assert.Single(order.Positions, position => position.IsListed);
        var unlisted = Assert.Single(order.Positions, position => !position.IsListed);

        Assert.Equal("rdp_zzz.archive", listed.FileName);
        Assert.True(listed.Rank < unlisted.Rank);
    }

    /// <summary>
    /// The unmeasured residue, carried rather than guessed.
    /// </summary>
    /// <remarks>
    /// Two archives a present list does not name share a rank, because the
    /// measurement never observed their order relative to each other. ASCII
    /// order is the natural guess, and this asserts the resolver does not make
    /// it - the archive that would win under that guess is not reported as
    /// winning.
    /// </remarks>
    [Fact]
    public void TwoUnlistedArchivesShareARankUnderAPresentList()
    {
        SyntheticArchive.Write(_directory, ArchiveA, ContestedPath);
        SyntheticArchive.Write(_directory, ArchiveB, ContestedPath);
        SyntheticArchive.Write(_directory, "rdp_listed.archive", @"base\rdp\listed.json");
        WriteModlist("rdp_listed.archive");

        var order = Order();

        Assert.False(order.IsFullyOrdered);
        Assert.Equal(
            order.PositionOf(ArchiveA)!.Value.Rank,
            order.PositionOf(ArchiveB)!.Value.Rank);
    }

    /// <summary>
    /// A list file with no names in it is not a directory without one.
    /// </summary>
    /// <remarks>
    /// What was measured is the presence of the file, so an empty one puts
    /// every archive in the unlisted group and orders none of them. Reading it
    /// as an absent list would restore file-name order, which is a branch of
    /// the law selected by a condition that does not hold.
    /// </remarks>
    [Fact]
    public void AnEmptyListFileIsPresentAndOrdersNothing()
    {
        SyntheticArchive.Write(_directory, ArchiveA, ContestedPath);
        SyntheticArchive.Write(_directory, ArchiveB, ContestedPath);
        WriteModlist(string.Empty);

        var order = Order();

        Assert.True(order.Modlist.IsPresent);
        Assert.Equal(0, order.Modlist.ListedCount);
        Assert.False(order.IsFullyOrdered);
        Assert.All(order.Positions, position => Assert.False(position.IsListed));
    }

    [Fact]
    public void ANameTheListGivesThatNoArchiveAnswersToIsReported()
    {
        SyntheticArchive.Write(_directory, ArchiveA, @"base\rdp\a.json");
        WriteModlist("rdp_departed.archive|" + ArchiveA);

        var order = Order();

        Assert.Equal(new[] { "rdp_departed.archive" }, order.ListedButNotPresent);
        Assert.True(order.PositionOf(ArchiveA)!.Value.IsListed);
    }

    [Fact]
    public void ANameTheListGivesTwiceIsHeldAtItsFirstPlaceAndReported()
    {
        SyntheticArchive.Write(_directory, ArchiveA, @"base\rdp\a.json");
        SyntheticArchive.Write(_directory, ArchiveB, @"base\rdp\b.json");
        WriteModlist(ArchiveA + "|" + ArchiveB + "|" + ArchiveA);

        var order = Order();

        Assert.Equal(new[] { ArchiveA }, order.Modlist.RepeatedNames);
        Assert.Equal(new[] { ArchiveA, ArchiveB }, order.Modlist.ListedNames);
        Assert.True(order.PositionOf(ArchiveA)!.Value.Rank < order.PositionOf(ArchiveB)!.Value.Rank);
    }

    /// <summary>
    /// A list entry spelled in another case names the archive it names.
    /// </summary>
    /// <remarks>
    /// The directory is a Windows path, so the two spellings are one file. A
    /// case-sensitive match would order this archive as unlisted and report a
    /// winner the game does not agree with, without saying anything was wrong.
    /// </remarks>
    /// <summary>
    /// A repeat spelled differently is reported at the spelling that was kept.
    /// </summary>
    /// <remarks>
    /// The repeat is the same name under the match rule, so reporting its own
    /// spelling would hand a caller a string that appears in neither the listed
    /// names nor the order, and a cross-reference by exact text would find
    /// nothing.
    /// </remarks>
    [Fact]
    public void ARepeatedNameIsReportedAtTheSpellingTheListKept()
    {
        var modlist = Modlist.Of(["A.archive", "a.archive"]);

        Assert.Equal(new[] { "A.archive" }, modlist.ListedNames);
        Assert.Equal(new[] { "A.archive" }, modlist.RepeatedNames);
        Assert.All(
            modlist.RepeatedNames,
            name => Assert.Contains(name, modlist.ListedNames, StringComparer.Ordinal));
    }

    [Fact]
    public void AListEntryMatchesAnArchiveWhoseNameDiffersOnlyInCase()
    {
        SyntheticArchive.Write(_directory, ArchiveA, @"base\rdp\a.json");
        WriteModlist(ArchiveA.ToUpperInvariant());

        var order = Order();

        Assert.True(order.PositionOf(ArchiveA)!.Value.IsListed);
        Assert.Empty(order.ListedButNotPresent);
    }

    [Fact]
    public void BlankLinesInTheListAreNotArchives()
    {
        SyntheticArchive.Write(_directory, ArchiveA, @"base\rdp\a.json");
        WriteModlist("|   |" + ArchiveA + "|");

        var order = Order();

        Assert.Equal(new[] { ArchiveA }, order.Modlist.ListedNames);
        Assert.Equal(0, order.PositionOf(ArchiveA)!.Value.Rank);
    }

    /// <summary>
    /// A line the list gives that this project cannot interpret still names
    /// something.
    /// </summary>
    /// <remarks>
    /// No comment syntax was measured, so a line beginning with a marker some
    /// other format would treat as a comment is read as a name like any other
    /// and surfaces as naming nothing. Skipping it would be an interpretation,
    /// and an interpretation that is wrong drops a line the game is honouring.
    /// </remarks>
    [Fact]
    public void ALineThatLooksLikeACommentIsReadAsANameAndSurfacesAsNamingNothing()
    {
        SyntheticArchive.Write(_directory, ArchiveA, @"base\rdp\a.json");
        WriteModlist("# rdp_note.archive|" + ArchiveA);

        var order = Order();

        Assert.Equal(new[] { "# rdp_note.archive" }, order.ListedButNotPresent);
        Assert.Equal(2, order.Modlist.ListedCount);
    }

    [Fact]
    public void AListFileTheCallerCannotReadIsAnnouncedAsItselfRatherThanAsAbsent()
    {
        Assert.Equal(
            ArchiveFailureKind.UnreadableModlist,
            ArchiveFailure.Classify(
                new UnauthorizedAccessException(),
                ArchiveFailureKind.UnreadableModlist,
                ArchiveOperation.FileRead));

        var message = ArchiveFailure.Describe(
            ArchiveFailureKind.UnreadableModlist,
            "somewhere",
            "it raised UnauthorizedAccessException: denied");

        // The sentence directs what happens next: a caller must not read this
        // as a directory that simply has no list.
        Assert.Contains("could not be read", message, StringComparison.Ordinal);
        Assert.Contains("no order is reported", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A list file held open by something else is refused as itself.
    /// </summary>
    /// <remarks>
    /// The likeliest way a list file that exists fails to be read is that
    /// another process has it, which raises no denial. Driven end to end
    /// rather than through the classifier, because what is held is the kind
    /// and sentence a caller actually receives.
    /// </remarks>
    [Fact]
    public void AListFileAnotherProcessHoldsOpenIsRefusedAsUnreadableRatherThanUnclassified()
    {
        File.WriteAllText(Path.Combine(_directory, Modlist.FileName), ArchiveA + Environment.NewLine);

        using var held = new FileStream(
            Path.Combine(_directory, Modlist.FileName),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        var thrown = Assert.Throws<ArchiveReadException>(() => Modlist.Read(_directory));

        Assert.Equal(ArchiveFailureKind.UnreadableModlist, thrown.Kind);
        Assert.IsAssignableFrom<IOException>(thrown.InnerException);
        Assert.Contains("could not be read", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be enumerated", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No failure of a file read is reported as an unclassified enumeration.
    /// </summary>
    /// <remarks>
    /// The claim the operation seam makes. A file read reaches the classifier
    /// only after the file was seen to exist, so there is no cause for which
    /// the caller should be handed the listing wording.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReadFailures))]
    public void NoCauseOfAFileReadFailureIsReportedAsAnUnclassifiedListing(Exception cause)
    {
        Assert.Equal(
            ArchiveFailureKind.UnreadableModlist,
            ArchiveFailure.Classify(cause, ArchiveFailureKind.UnreadableModlist, ArchiveOperation.FileRead));
    }

    public static IEnumerable<object[]> ReadFailures() =>
    [
        [new IOException("held open")],
        [new UnauthorizedAccessException("denied")],
        [new FileNotFoundException("removed between the check and the read")],
        [new DirectoryNotFoundException("the directory went away")],
        [new System.Security.SecurityException("refused by policy")],
        [new NotSupportedException("the path names something unreadable")],
    ];

    private void WriteTheThreeArchives()
    {
        SyntheticArchive.Write(_directory, ArchiveA, ContestedPath, @"base\rdp\a_only.json");
        SyntheticArchive.Write(_directory, ArchiveB, ContestedPath, @"base\rdp\b_only.json");
        SyntheticArchive.Write(_directory, Detector, @"base\rdp\detector.json");
    }

    /// <summary>
    /// Writes the list file, or leaves the directory without one.
    /// </summary>
    /// <param name="listedNames">
    /// The lines, separated by a bar, or <see langword="null" /> for a
    /// directory with no list file at all. The two are different branches of
    /// the law, so the fixture has to be able to express both.
    /// </param>
    private void WriteModlist(string? listedNames)
    {
        var path = Path.Combine(_directory, Modlist.FileName);
        if (listedNames is null)
        {
            File.Delete(path);
            return;
        }

        File.WriteAllLines(path, listedNames.Split('|'));
    }

    private ArchiveLoadOrder Order() =>
        ArchiveLoadOrder.Of(
            new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(_directory),
            Modlist.Read(_directory));

    private ContestedSet Resolve()
    {
        var inventory = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(_directory);
        return ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, Modlist.Read(_directory)));
    }
}
