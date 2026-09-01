using Ripperdoc.Core.Diagnosis;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The partition of wanted against deployed, over records this project wrote.
/// </summary>
/// <remarks>
/// The identities are invented. What the partition turns on is whether a mod's
/// identity appears on both sides, and no real mod is needed to say that - so
/// none is here.
/// </remarks>
public sealed class DeploymentPartitionTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ripperdoc-partition-tests-" + Guid.NewGuid().ToString("N"));

    public DeploymentPartitionTests() => Directory.CreateDirectory(_directory);

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
    /// The measured shape: every wanted mod deployed but a container that
    /// declares no deployable content, which is named as such rather than
    /// counted.
    /// </summary>
    [Fact]
    public void AContainerThatDeploysNothingIsMissingWithItsKindNamed()
    {
        var partition = DeploymentPartition.Of(
            [Mod("alpha"), Mod("beta"), new ManagerMod("the-list", Enabled: true, Kind: "collection")],
            Record(("alpha", "r6/scripts/a.reds"), ("beta", "archive/pc/mod/b.archive")));

        Assert.Equal(2, partition.Count(PartitionBucket.Deployed));
        Assert.Equal(1, partition.Count(PartitionBucket.Missing));
        Assert.Equal(0, partition.Count(PartitionBucket.Unresolvable));
        Assert.Equal(0, partition.Count(PartitionBucket.Unclaimed));

        var container = Assert.Single(partition.Mods, mod => mod.Bucket == PartitionBucket.Missing);
        Assert.Equal("the-list", container.Id);
        Assert.Contains("collection", container.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no deployment record, nothing is reported missing and everything is
    /// reported unresolvable.
    /// </summary>
    /// <remarks>
    /// The distinction the bucket exists for. A game directory with no record is
    /// the ordinary state of one the manager has purged, and differencing the
    /// wanted set against an empty deployed set would report every mod as
    /// missing - arithmetic that works and an answer that is false.
    /// </remarks>
    [Fact]
    public void WithNoRecordEveryWantedModIsUnresolvableRatherThanMissing()
    {
        var partition = DeploymentPartition.Of([Mod("alpha"), Mod("beta")], record: null);

        Assert.False(partition.RecordWasRead);
        Assert.Equal(2, partition.Count(PartitionBucket.Unresolvable));
        Assert.Equal(0, partition.Count(PartitionBucket.Missing));
        Assert.Equal(0, partition.Count(PartitionBucket.Deployed));
        Assert.All(partition.Mods, mod =>
            Assert.Contains("no deployment record", mod.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    /// A mod the record deploys and the profile does not ask for is unclaimed.
    /// </summary>
    [Fact]
    public void AModDeployedWithoutBeingWantedIsUnclaimed()
    {
        var partition = DeploymentPartition.Of(
            [Mod("alpha"), new ManagerMod("stale", Enabled: false, Kind: "")],
            Record(("alpha", "r6/scripts/a.reds"), ("stale", "r6/scripts/s.reds")));

        Assert.Equal(1, partition.Count(PartitionBucket.Deployed));
        var unclaimed = Assert.Single(partition.Mods, mod => mod.Bucket == PartitionBucket.Unclaimed);
        Assert.Equal("stale", unclaimed.Id);
    }

    /// <summary>
    /// A mod the profile does not ask for and nothing deployed is not in the
    /// partition at all.
    /// </summary>
    /// <remarks>
    /// Beside the row above, which is the same mod deployed. A disabled mod that
    /// deployed nothing is neither wanted nor present, so reporting it would put
    /// a mod in a diagnosis on the strength of the manager merely knowing it.
    /// </remarks>
    [Fact]
    public void AModNeitherWantedNorDeployedIsNotReported()
    {
        var partition = DeploymentPartition.Of(
            [Mod("alpha"), new ManagerMod("stale", Enabled: false, Kind: "")],
            Record(("alpha", "r6/scripts/a.reds")));

        Assert.Equal(["alpha"], partition.Mods.Select(mod => mod.Id));
    }

    /// <summary>
    /// Every mod on either side comes out exactly once, and in one bucket.
    /// </summary>
    /// <remarks>
    /// The exhaustiveness claim, read rather than asserted of the design. The
    /// arrangement carries one mod of every kind at once - deployed, missing,
    /// unclaimed, and a disabled mod that is neither - so a partition that
    /// dropped any one case would come out short here.
    /// </remarks>
    [Fact]
    public void EveryModOnEitherSideComesOutExactlyOnce()
    {
        ManagerMod[] known =
        [
            Mod("deployed-one"),
            Mod("deployed-two"),
            Mod("missing-one"),
            new ManagerMod("unclaimed-one", Enabled: false, Kind: ""),
            new ManagerMod("absent-and-unwanted", Enabled: false, Kind: ""),
        ];

        var record = Record(
            ("deployed-one", "r6/scripts/a.reds"),
            ("deployed-two", "archive/pc/mod/b.archive"),
            ("deployed-two", "archive/pc/mod/b.xl"),
            ("unclaimed-one", "r6/scripts/u.reds"));

        var partition = DeploymentPartition.Of(known, record);

        var expected = known.Where(mod => mod.Enabled).Select(mod => mod.Id)
            .Union(record.Files.Select(file => file.SourceMod), StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(expected, partition.Mods.Select(mod => mod.Id));
        Assert.Equal(partition.Mods.Count, partition.Mods.Select(mod => mod.Id).Distinct().Count());
        Assert.Equal(
            partition.Mods.Count,
            Enum.GetValues<PartitionBucket>().Sum(partition.Count));
        Assert.All(partition.Mods, mod => Assert.NotEqual(string.Empty, mod.Reason));
    }

    /// <summary>The reading does not depend on the order the inputs arrive.</summary>
    [Fact]
    public void TheOrderOfTheInputsDoesNotChangeTheReading()
    {
        ManagerMod[] known = [Mod("c"), Mod("a"), Mod("b")];
        var record = Record(("b", "one"), ("a", "two"));

        var forwards = DeploymentPartition.Of(known, record);
        var backwards = DeploymentPartition.Of([.. known.Reverse()], record);

        Assert.Equal(forwards.Mods, backwards.Mods);
        Assert.Equal(["a", "b", "c"], forwards.Mods.Select(mod => mod.Id));
    }

    /// <summary>A game directory with no record reads as none, not as an error.</summary>
    [Fact]
    public void AGameDirectoryWithNoRecordReadsAsNone()
    {
        Assert.Null(DeploymentRecord.In(_directory));
    }

    /// <summary>A record on disk is read, with its method and game carried.</summary>
    [Fact]
    public void ARecordOnDiskIsRead()
    {
        File.WriteAllText(
            Path.Combine(_directory, DeploymentRecord.FileName),
            """
            {
              "deploymentMethod": "hardlink_activator",
              "gameId": "a-game",
              "files": [ { "relPath": "r6/scripts/a.reds", "source": "alpha", "time": 1 } ]
            }
            """);

        var record = DeploymentRecord.In(_directory);

        Assert.NotNull(record);
        Assert.Equal("hardlink_activator", record.Method);
        Assert.Equal("a-game", record.GameId);
        Assert.Equal(new DeployedFile("r6/scripts/a.reds", "alpha"), Assert.Single(record.Files));
    }

    /// <summary>
    /// A record this reader does not understand is refused rather than read as
    /// a partial one.
    /// </summary>
    /// <remarks>
    /// Each of these would otherwise produce a shorter deployed side, and a
    /// shorter deployed side reports wanted mods as missing. A diagnosis
    /// assembled from a half-parsed record names real mods and blames them for
    /// a failure that is this reader's.
    /// </remarks>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{ "gameId": "a-game" }""")]
    [InlineData("""{ "files": {} }""")]
    [InlineData("""{ "files": [ { "relPath": "a" } ] }""")]
    [InlineData("""{ "files": [ { "source": "alpha" } ] }""")]
    [InlineData("""{ "files": [ { "relPath": "a", "source": 7 } ] }""")]
    public void ARecordThatCannotBeReadIsRefused(string json)
    {
        Assert.Throws<DiagnosisReadException>(() => DeploymentRecord.Parse(json, "a record"));
    }

    /// <summary>Neither entry point takes a null.</summary>
    [Fact]
    public void TheReadersRefuseAMissingArgument()
    {
        Assert.Throws<ArgumentNullException>(() => DeploymentRecord.In(null!));
        Assert.Throws<ArgumentNullException>(() => DeploymentRecord.Parse(null!, "a record"));
        Assert.Throws<ArgumentNullException>(() => DeploymentRecord.Parse("{}", null!));
        Assert.Throws<ArgumentNullException>(() => DeploymentPartition.Of(null!, null));
    }

    private static ManagerMod Mod(string id) => new(id, Enabled: true, Kind: "");

    private static DeploymentRecord Record(params (string Mod, string Path)[] files) =>
        new("hardlink_activator", "a-game", "/game",
            [.. files.Select(file => new DeployedFile(file.Path, file.Mod))]);
}
