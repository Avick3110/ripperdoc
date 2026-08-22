using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

// The ordering law these check is a measurement, and the fixtures encode it
// rather than someone's recollection of it: findings/2026-08-22-tweak-file-order-groups.md,
// which supersedes findings/2026-08-19-tweak-file-order.md and carries the
// evidence class of each part.
public class TweakLayerTests
{
    [Fact]
    public void ALinkBackIntoTheLayerIsRefusedByNameRatherThanWalkedForever()
    {
        using var layer = SyntheticTweakLayer.Of(
            ("mods\\alpha.yaml", "Probe.item.price: 100\n"),
            ("mods\\beta.yaml", "Probe.item.price: 250\n"));

        var link = Path.Combine(layer.Root, "mods", "shared");
        DirectoryLink.Create(link, layer.Root);

        try
        {
            // Without the guard this does not fail - it does not return, and
            // the process is gone on a stack overflow that cannot be caught,
            // with nothing said about which entry did it.
            var walked = layer.Enumerate();

            var refused = Assert.Single(walked.Refused);
            Assert.Equal("mods\\shared", refused.Path);
            Assert.Contains("already inside", refused.Reason, StringComparison.Ordinal);

            // The rest of the layer is still walked. Refusing an entry costs
            // what is behind that entry and nothing else.
            Assert.Equal(
                new[] { "mods\\alpha.yaml", "mods\\beta.yaml" },
                walked.Files.Select(file => file.RelativePath).OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            DirectoryLink.Remove(link);
        }
    }

    [Fact]
    public void ALinkToADirectoryTheWalkIsNotInsideIsFollowedRatherThanRefused()
    {
        // The other arm. A link is not a cycle just for being a link, and one
        // pointing somewhere the walk is not already inside is a directory the
        // framework reads - refusing it would drop files that are in the game.
        using var layer = SyntheticTweakLayer.Of(
            ("mods\\alpha.yaml", "Probe.item.price: 100\n"),
            ("elsewhere\\beta.yaml", "Probe.item.price: 250\n"));

        var link = Path.Combine(layer.Root, "mods", "shared");
        DirectoryLink.Create(link, Path.Combine(layer.Root, "elsewhere"));

        try
        {
            var walked = layer.Enumerate();

            Assert.Empty(walked.Refused);
            Assert.Contains(
                "mods\\shared\\beta.yaml",
                walked.Files.Select(file => file.RelativePath),
                StringComparer.Ordinal);
        }
        finally
        {
            DirectoryLink.Remove(link);
        }
    }

    [Fact]
    public void ARefusedEntryIsCarriedIntoWhatTheResolvedStateDoesNotAccountFor()
    {
        // A layer that quietly stops short reports the mods behind the refusal
        // as having written nothing, which is a wrong answer with nothing in
        // the report to contradict it.
        var refused = new TweakUnhandled(1, "mods\\shared", "a link back into a directory this walk is already inside");
        var walked = TweakLayer.Of(["mods\\alpha.yaml"], enumerationIsCollated: true, [refused]);

        var state = TweakResolvedState.Replay(
            walked,
            [new TweakDocument("mods\\alpha.yaml", [], IsReadable: true)],
            TweakInheritanceMap.None,
            values: null);

        Assert.Equal("mods\\shared", Assert.Single(state.Unhandled).Path);
    }

    // The walk is checked against a real directory, because a walk checked
    // against a list of paths is a check of the list. What it must NOT do is
    // assert the order the directory came back in: that order belongs to the
    // volume, and the framework consumes it rather than sorting it, so a check
    // that writes files and then expects them back in name order is asserting a
    // property of whichever filesystem it happens to run on. Everything the
    // walk owes is stated here relative to the enumeration it was given.
    private static string[] TopLevelRunsOf(TweakLayer layer) => layer.Present
        .Select(file => file.RelativePath.Split(TweakFile.PathSeparator)[0])
        .Aggregate(new List<string>(), (runs, name) =>
        {
            if (runs.Count == 0 || runs[^1] != name)
            {
                runs.Add(name);
            }

            return runs;
        })
        .ToArray();

    [Fact]
    public void ADirectorysContentsAreReadAtTheDirectorysOwnPositionRatherThanAfterItsSiblings()
    {
        using var layer = SyntheticTweakLayer.OfEmpty(
            "aaa.yaml",
            "mmm\\inner.yaml",
            "mmm0.yaml",
            "zzz.yaml");

        var walked = layer.Enumerate();
        var entries = Directory.EnumerateFileSystemEntries(layer.Root).Select(Path.GetFileName).ToArray();

        // Each top-level entry occupies one unbroken run in the walk, and those
        // runs come in the order the directory gave the entries. A directory
        // whose contents were emitted after its siblings, or split around them,
        // breaks this whatever order the volume hands back - and the subdirectory
        // here is the one that would move, because mmm0.yaml sorts before
        // mmm\\inner.yaml under any full-path comparison and is read after it.
        Assert.Equal(entries, TopLevelRunsOf(walked));
        Assert.Equal(4, walked.Files.Count);
    }

    [Fact]
    public void FilesAndSubdirectoriesAreOrderedTogetherRatherThanInTwoPasses()
    {
        using var layer = SyntheticTweakLayer.OfEmpty(
            "aaa.yaml",
            "mmm\\inner.yaml",
            "zzz.yaml");

        var walked = layer.Enumerate();
        var entries = Directory.EnumerateFileSystemEntries(layer.Root).Select(Path.GetFileName).ToArray();

        // A files-first pass would put both root files before the subdirectory's
        // whatever the enumeration said, and a directories-first pass would put
        // the subdirectory's before both. Either one reorders the runs away from
        // the order the directory gave them.
        Assert.Equal(entries, TopLevelRunsOf(walked));
    }

    [Fact]
    public void TheGroupingAndThePositionsFollowTheWalkGivenRatherThanAnyOrderOfTheirOwn()
    {
        // The same walk in two orders, neither of them sorted. What comes out
        // has to be the grouping applied to the order it was given - so the
        // second case is the one that matters: an engine that sorted anywhere
        // would return the same answer twice, and on a volume whose enumeration
        // is not collated it would then disagree with the game.
        string[] collated = ["aaa.yaml", "mmm.yaml", "zzz.yaml"];
        string[] notCollated = ["zzz.yaml", "aaa.yaml", "mmm.yaml"];

        Assert.Equal(collated, TweakLayer.Of(collated, enumerationIsCollated: true)
            .Files.Select(file => file.RelativePath));
        Assert.Equal(notCollated, TweakLayer.Of(notCollated, enumerationIsCollated: false)
            .Files.Select(file => file.RelativePath));
    }

    [Fact]
    public void TheGroupingOverrulesAWalkThatIsNotCollatedJustAsItOverrulesOneThatIs()
    {
        // Grouping is the framework's rule and the walk order is the volume's,
        // so the first must apply on top of the second whatever the second is.
        string[] notCollated = ["zzz\\^held_back.yaml", "mmm.yaml", "aaa\\_promoted.yaml"];

        var layer = TweakLayer.Of(notCollated, enumerationIsCollated: false);

        Assert.Equal(
            new[] { "aaa\\_promoted.yaml", "mmm.yaml", "zzz\\^held_back.yaml" },
            layer.Files.Select(file => file.RelativePath));
        Assert.Equal(new[] { 1, 2, 3 }, layer.Files.Select(file => file.ReadPosition));
        Assert.False(layer.EnumerationIsCollated);
    }

    [Theory]
    [InlineData("_marked.yaml")]
    [InlineData("#marked.yaml")]
    [InlineData("$marked.yaml")]
    [InlineData("!marked.yaml")]
    public void AFileWhoseOwnNameStartsWithAFirstGroupMarkerIsReadBeforeEverythingElse(string marked)
    {
        using var layer = SyntheticTweakLayer.OfEmpty("aaa\\first_by_walk.yaml", "zzz\\" + marked);

        var read = layer.Enumerate().Files.Select(file => file.RelativePath).ToArray();

        // The walk reaches aaa\ first. The grouping overrules it.
        Assert.Equal(new[] { "zzz\\" + marked, "aaa\\first_by_walk.yaml" }, read);
    }

    [Fact]
    public void AFileWhoseOwnNameStartsWithTheLastGroupMarkerIsReadAfterEverythingElse()
    {
        using var layer = SyntheticTweakLayer.OfEmpty("aaa\\^held_back.yaml", "zzz\\ordinary.yaml");

        var read = layer.Enumerate().Files.Select(file => file.RelativePath).ToArray();

        Assert.Equal(new[] { "zzz\\ordinary.yaml", "aaa\\^held_back.yaml" }, read);
    }

    [Fact]
    public void TheGroupIsTakenFromTheFilesOwnNameAndNotFromAnyDirectoryAboveIt()
    {
        using var layer = SyntheticTweakLayer.OfEmpty(
            "#marked_directory\\ordinary.yaml",
            "zzz\\_marked_leaf.yaml");

        var read = layer.Enumerate().Files.Select(file => file.RelativePath).ToArray();

        // The marked directory sorts first in the walk and gets no promotion
        // from it; the marked leaf, deep in a directory that sorts last, does.
        Assert.Equal(new[] { "zzz\\_marked_leaf.yaml", "#marked_directory\\ordinary.yaml" }, read);
        Assert.Equal(TweakFileGroup.Normal, TweakLayer.GroupOf("#marked_directory\\ordinary.yaml"));
        Assert.Equal(TweakFileGroup.First, TweakLayer.GroupOf("zzz\\_marked_leaf.yaml"));
    }

    [Theory]
    [InlineData("zzz\\_promoted.yaml", TweakFileGroup.First)]
    [InlineData("#directory\\ordinary.yaml", TweakFileGroup.Normal)]
    [InlineData("zzz/_not_promoted.yaml", TweakFileGroup.Normal)]
    [InlineData("#directory/ordinary.yaml", TweakFileGroup.First)]
    public void TheGroupIsDecidedByTheLayersSeparatorAndNotByTheRunningPlatforms(
        string relativePath,
        TweakFileGroup expected)
    {
        // A layer path is spelled with one separator wherever it is read, so
        // the grouping has to come out the same on every platform: a report
        // naming one winner on one machine and another elsewhere is worth
        // nothing. Asking the running platform which characters divide a path
        // gets that wrong in both directions, and the two directions are only
        // reachable on different platforms - so both are checked here.
        //
        // The first two are the real inputs, and they are the ones that come
        // apart where '\' is an ordinary character: the whole path reads as
        // the file's own name and the leading character of the top directory
        // decides the group. The last two are the mirror, and they are the
        // ones this platform can reach: '/' divides a path here and does not
        // divide a layer path anywhere, so a name carrying it must be taken
        // whole. Neither pair fails on both platforms; together they fail on
        // either.
        Assert.Equal(expected, TweakLayer.GroupOf(relativePath));
    }

    [Fact]
    public void AMarkedDirectoryStillLeadsTheWalkSoTheTwoMechanismsCompose()
    {
        // Two files here are in the same group, so what separates them is the
        // walk - and the walk's order belongs to the volume. It is stated
        // rather than produced by writing files and hoping, because the point
        // being made is that the grouping composes with whatever order it is
        // handed, not that a particular filesystem hands back a particular one.
        string[] walked =
        [
            "#marked_directory\\#marked_leaf.yaml",
            "#marked_directory\\ordinary.yaml",
            "aaa\\ordinary.yaml",
        ];

        var read = TweakLayer.Of(walked, enumerationIsCollated: true)
            .Files.Select(file => file.RelativePath).ToArray();

        // The leaf is promoted by its own name; its sibling is not, and keeps
        // the lead its directory's name gives it in the walk.
        Assert.Equal(walked, read);
    }

    [Fact]
    public void ReadPositionsRunFromOneWithoutGapsInTheOrderTheFilesAreApplied()
    {
        using var layer = SyntheticTweakLayer.OfEmpty(
            "aaa\\ordinary.yaml",
            "aaa\\^last.yaml",
            "aaa\\_first.yaml",
            "notes.txt");

        var files = layer.Enumerate().Files;

        Assert.Equal(new[] { 1, 2, 3 }, files.Select(file => file.ReadPosition));
        Assert.Equal(new[] { "aaa\\_first.yaml", "aaa\\ordinary.yaml", "aaa\\^last.yaml" },
            files.Select(file => file.RelativePath));
    }

    [Theory]
    [InlineData("thing.yaml", TweakFileFormat.Yaml)]
    [InlineData("thing.yml", TweakFileFormat.Yaml)]
    [InlineData("thing.tweak", TweakFileFormat.Red)]
    [InlineData("thing.YAML", TweakFileFormat.NotRead)]
    [InlineData("thing.Yml", TweakFileFormat.NotRead)]
    [InlineData("thing.archive", TweakFileFormat.NotRead)]
    [InlineData("thing", TweakFileFormat.NotRead)]
    public void TheExtensionDecidesWhichReaderAFileGoesTo(string name, TweakFileFormat expected) =>
        Assert.Equal(expected, TweakLayer.FormatOf(name));

    [Fact]
    public void AFilePresentButNotReadIsCarriedRatherThanDroppedBetweenTheDirectoryAndTheReport()
    {
        using var layer = SyntheticTweakLayer.OfEmpty("thing.yaml", "notes.txt", "sub\\art.archive");

        var enumerated = layer.Enumerate();

        Assert.Equal(new[] { "thing.yaml" }, enumerated.Files.Select(file => file.RelativePath));
        Assert.Equal(new[] { "notes.txt", "sub\\art.archive" },
            enumerated.Unread.Select(file => file.RelativePath).OrderBy(path => path, StringComparer.Ordinal));
        Assert.Equal(3, enumerated.Present.Count);
        Assert.All(enumerated.Unread, file => Assert.Equal(0, file.ReadPosition));
    }

    [Fact]
    public void TheOtherTweakLanguageIsNamedAsAFormatRatherThanTreatedAsUnreadable()
    {
        using var layer = SyntheticTweakLayer.OfEmpty("thing.tweak");

        var file = Assert.Single(layer.Enumerate().Files);

        // The framework reads it; this engine does not replay it. Those are
        // different facts and a report that merges them would say the game
        // ignores a file it acts on.
        Assert.Equal(TweakFileFormat.Red, file.Format);
    }

    [Fact]
    public void TheCollationFlagReportsTheEnumerationRatherThanAConstant()
    {
        // Asserting "collated" of a real directory asserts something about the
        // volume, not about this engine: some filesystems hand entries back in
        // a collation and some do not, and the whole point of this flag is that
        // the engine checks instead of assuming. So what is asserted is that it
        // reports what the enumeration actually was.
        using var layer = SyntheticTweakLayer.OfEmpty("aaa.yaml", "bbb.yaml", "ccc.yaml");

        var entries = Directory.EnumerateFileSystemEntries(layer.Root).ToArray();

        Assert.Equal(TweakLayer.IsCollated(entries), layer.Enumerate().EnumerationIsCollated);
    }

    [Fact]
    public void ALayerOfOneFileIsCollatedOnAnyVolume()
    {
        // The one arm that is a fact about the engine rather than about the
        // filesystem: a single entry cannot be out of order with anything, so
        // this holds wherever it runs and fails if the flag is hardcoded false
        // or dropped.
        using var layer = SyntheticTweakLayer.OfEmpty("only.yaml");

        Assert.True(layer.Enumerate().EnumerationIsCollated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ALayerCarriesTheCollationItWasBuiltWith(bool collated)
    {
        // Both answers reach the layer, which is what a caller reading a report
        // depends on. Through a directory the false one is unreachable on a
        // volume that collates, so it is stated here instead of left as a branch
        // nobody has seen carried.
        Assert.Equal(collated, TweakLayer.Of(["only.yaml"], collated).EnumerationIsCollated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothAnswersAboutTheCollationAreReachable(bool collated)
    {
        // The false arm decides whether an ordering conclusion over a layer can
        // be stated plainly, and no directory on a case-insensitively collated
        // volume can produce it - so it is checked against a sequence directly
        // rather than left as a branch nobody has seen run.
        string[] entries = collated
            ? ["aaa.yaml", "Bbb.yaml", "ccc.yaml"]
            : ["ccc.yaml", "aaa.yaml"];

        Assert.Equal(collated, TweakLayer.IsCollated(entries));
    }

    [Fact]
    public void CollationIsJudgedWithoutRegardToCase()
    {
        // Case-sensitive ordinal would call this pair out of order; the
        // measurement the walk implements was taken under a case-insensitive
        // collation, so this is the comparison that has to agree.
        Assert.True(TweakLayer.IsCollated(new[] { "apple.yaml", "Banana.yaml" }));
        Assert.True(TweakLayer.IsCollated(Array.Empty<string>()));
        Assert.True(TweakLayer.IsCollated(new[] { "only.yaml" }));
    }

    [Fact]
    public void EveryShapeOfFileALayerCanHoldIsAccountedForOneWayOrAnother()
    {
        // The rule this exercises is asserted against a real install, which no
        // runner has - so without this it would only ever run on a machine with
        // the game on it, against whichever shapes that one install happens to
        // contain. An empty file and a file of nothing but comments are ordinary
        // things to ship and neither yields a statement; counted as unaccounted
        // they turn an install nobody has done anything wrong with into a red
        // run.
        using var layer = SyntheticTweakLayer.Of(
            ("alpha\\writes.yaml", "Probe.thing.amount: 1\n"),
            ("alpha\\empty.yaml", ""),
            ("alpha\\comments.yaml", "# nothing but a comment\n"),
            ("alpha\\broken.yaml", "Probe.thing: [unclosed\n"),
            ("alpha\\instruction.yaml", "$game: 2.31\n"),
            ("alpha\\notes.txt", "not a tweak file at all\n"));

        var enumerated = layer.Enumerate();
        var documents = TweakFileReader.ReadLayer(enumerated, layer.Root);
        var reported = new List<string>();

        Assert.Empty(InstalledTweakLayerTests.FilesNeitherReadFromNorNamed(
            enumerated,
            documents,
            reported.Add));

        // And the two that hold nothing are accounted as that rather than as
        // something read, which is the distinction the rule turns on.
        Assert.Equal(
            new[] { "alpha\\comments.yaml", "alpha\\empty.yaml" },
            reported.Select(line => line.Replace("  read and empty: ", string.Empty)).OrderBy(
                path => path,
                StringComparer.Ordinal));
    }

    [Fact]
    public void EnumeratingSomethingThatIsNotThereFailsByName()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-tweak-layer-" + Guid.NewGuid().ToString("N")[..12]);

        var exception = Assert.Throws<DirectoryNotFoundException>(() => TweakLayer.Enumerate(missing));

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnumeratingNothingIsNotAnError()
    {
        using var layer = SyntheticTweakLayer.OfEmpty();

        var enumerated = layer.Enumerate();

        Assert.Empty(enumerated.Files);
        Assert.Empty(enumerated.Present);
        Assert.True(enumerated.EnumerationIsCollated);
    }
}
