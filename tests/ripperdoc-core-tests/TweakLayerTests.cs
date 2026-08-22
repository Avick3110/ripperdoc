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
