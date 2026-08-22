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
    public void ADirectorysContentsAreReadAtTheDirectorysOwnPositionRatherThanAfterItsSiblings()
    {
        using var layer = SyntheticTweakLayer.OfEmpty(
            "aaa.yaml",
            "mmm\\inner.yaml",
            "mmm0.yaml",
            "zzz.yaml");

        var read = layer.Enumerate().Files.Select(file => file.RelativePath).ToArray();

        // mmm0.yaml sorts before mmm\inner.yaml under any full-path comparison,
        // because '0' is below '\'. It is read after, because the directory's
        // contents are taken where the directory itself sits.
        Assert.Equal(new[] { "aaa.yaml", "mmm\\inner.yaml", "mmm0.yaml", "zzz.yaml" }, read);
    }

    [Fact]
    public void FilesAndSubdirectoriesAreOrderedTogetherRatherThanInTwoPasses()
    {
        using var layer = SyntheticTweakLayer.OfEmpty(
            "aaa.yaml",
            "mmm\\inner.yaml",
            "zzz.yaml");

        var read = layer.Enumerate().Files.Select(file => file.RelativePath).ToArray();

        // A files-first pass would put both root files before the subdirectory's,
        // and a directories-first pass would put the subdirectory's before both.
        Assert.Equal(new[] { "aaa.yaml", "mmm\\inner.yaml", "zzz.yaml" }, read);
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
        using var layer = SyntheticTweakLayer.OfEmpty(
            "#marked_directory\\#marked_leaf.yaml",
            "#marked_directory\\ordinary.yaml",
            "aaa\\ordinary.yaml");

        var read = layer.Enumerate().Files.Select(file => file.RelativePath).ToArray();

        // The leaf is promoted by its own name; its sibling is not, and keeps
        // the lead its directory's name gives it in the walk.
        Assert.Equal(new[] { "#marked_directory\\#marked_leaf.yaml", "#marked_directory\\ordinary.yaml", "aaa\\ordinary.yaml" },
            read);
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
    public void AnEnumerationThatIsAlreadyCollatedSaysSo()
    {
        using var layer = SyntheticTweakLayer.OfEmpty("aaa.yaml", "bbb\\inner.yaml", "ccc.yaml");

        Assert.True(layer.Enumerate().EnumerationIsCollated);
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
