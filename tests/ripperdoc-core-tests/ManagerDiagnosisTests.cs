using Ripperdoc.Core.Diagnosis;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The readers wired to their inputs, and what the composition says about the
/// homes it could not read.
/// </summary>
public sealed class ManagerDiagnosisTests : IDisposable
{
    private const string Game = "a-game";
    private const string Profile = "profile-one";
    private const string Container = "mod-list";

    private readonly string staging =
        Directory.CreateTempSubdirectory("ripperdoc-staging-").FullName;

    private readonly string gameDirectory =
        Directory.CreateTempSubdirectory("ripperdoc-game-").FullName;

    public void Dispose()
    {
        Directory.Delete(staging, recursive: true);
        Directory.Delete(gameDirectory, recursive: true);
    }

    [Fact]
    public void EveryHomeReadIsNamedAndThePartitionIsOverTheStatesOwnWantedSet()
    {
        using var scratch = State();
        Manifest(Rule("before", "hash-of-b", "hash-of-a"));
        Record(("mods/a.archive", "mod-a"));

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Equal(2, diagnosis.Ordering.HomesRead.Count);
        Assert.Empty(diagnosis.Ordering.HomesNotRead);
        Assert.Null(diagnosis.WhyNoPartition);
        Assert.Equal(3, diagnosis.Partition!.Mods.Count);
        Assert.Equal(1, diagnosis.Partition.Count(PartitionBucket.Deployed));
        Assert.Equal(2, diagnosis.Partition.Count(PartitionBucket.Missing));
        Assert.True(diagnosis.Partition.RecordWasRead);
    }

    /// <summary>
    /// The two rule homes land on one node space, so a cycle running through
    /// both is found.
    /// </summary>
    /// <remarks>
    /// The claim this discriminates is that the manifest's file-named sides are
    /// carried to the manager's own ids. Keyed on anything else the two homes
    /// would be disjoint graphs and this cycle would not exist in either.
    /// </remarks>
    [Fact]
    public void ACycleRunningThroughBothRuleHomesIsFound()
    {
        using var scratch = State(stateRule: """{"type":"after","reference":{"id":"mod-b"}}""");
        Manifest(Rule("before", "hash-of-a", "hash-of-b"));

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Equal(2, diagnosis.Ordering.EdgeCount);
        Assert.Equal(2, diagnosis.Ordering.NodeCount);

        var cycle = Assert.Single(diagnosis.Ordering.Cycles);

        Assert.Equal(["mod-a", "mod-b", "mod-a"], cycle.Path);
    }

    /// <summary>
    /// Either home alone is acyclic, so the cycle above is one the composition
    /// found and neither reader could have.
    /// </summary>
    [Fact]
    public void NeitherRuleHomeAloneCarriesThatCycle()
    {
        using var scratch = State(stateRule: """{"type":"after","reference":{"id":"mod-b"}}""");
        Manifest(Rule("before", "hash-of-a", "hash-of-b"));

        var directory = scratch.Write();
        var reading = ManagerStateReading.Of(directory, Game)!;
        var manifest = CollectionManifest.In(
            CollectionManifest.PathsIn(reading)[0], reading)!;

        Assert.Empty(OrderingGraph.Over([reading.Rules], []).Cycles);
        Assert.Empty(OrderingGraph.Over([manifest.Rules], []).Cycles);
    }

    [Fact]
    public void WithNoStateThereIsNoPartitionRatherThanAnEmptyOne()
    {
        var absent = Path.Combine(Path.GetTempPath(), "ripperdoc-none-" + Guid.NewGuid().ToString("N"));
        Record(("mods/a.archive", "mod-a"));

        var diagnosis = ManagerDiagnosis.Of(absent, Game, gameDirectory);

        Assert.Null(diagnosis.Partition);
        Assert.Null(diagnosis.State);
        Assert.Contains("which mods were wanted", diagnosis.WhyNoPartition!, StringComparison.Ordinal);
        Assert.Equal(2, diagnosis.Ordering.HomesNotRead.Count);
        Assert.Empty(diagnosis.Ordering.HomesRead);
    }

    [Fact]
    public void WithNoDeploymentRecordEveryWantedModIsUnresolvable()
    {
        using var scratch = State();
        Manifest(Rule("before", "hash-of-b", "hash-of-a"));

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Null(diagnosis.Record);
        Assert.False(diagnosis.Partition!.RecordWasRead);
        Assert.Equal(
            diagnosis.Partition.Mods.Count,
            diagnosis.Partition.Count(PartitionBucket.Unresolvable));
        Assert.All(
            diagnosis.Partition.Mods,
            mod => Assert.Contains(
                "carries no deployment record", mod.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void AStagedListWithNoManifestIsNamedAsAHomeNotRead()
    {
        using var scratch = State();

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        var unread = Assert.Single(diagnosis.Ordering.HomesNotRead);

        Assert.Contains(Container, unread.Home, StringComparison.Ordinal);
        Assert.Contains("no manifest in it", unread.Reason, StringComparison.Ordinal);
        Assert.Single(diagnosis.Ordering.HomesRead);
    }

    [Fact]
    public void AStagedListWhoseManifestIsRefusedIsNamedOnce()
    {
        using var scratch = State();
        var directory = Path.Combine(staging, Container);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, CollectionManifest.FileName), "not json at all");

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        var unread = Assert.Single(diagnosis.Ordering.HomesNotRead);

        Assert.Contains(Container, unread.Home, StringComparison.Ordinal);
        Assert.DoesNotContain("no manifest in it", unread.Reason, StringComparison.Ordinal);
    }

    /// <remarks>
    /// A directory standing where the manifest belongs is a file the platform
    /// refuses to open rather than one that is not there, which is the same
    /// door a permission or a share denies - and it needs no privilege to set
    /// up, so the arm runs wherever the suite does.
    /// </remarks>
    [Fact]
    public void AManifestThatCannotBeOpenedIsRefusedRatherThanTakenForAbsent()
    {
        using var scratch = State();
        Directory.CreateDirectory(
            Path.Combine(staging, Container, CollectionManifest.FileName));

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        var unread = Assert.Single(diagnosis.Ordering.HomesNotRead);

        Assert.Contains("could not be read", unread.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("no manifest in it", unread.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused read becomes the reason its home is named as unread, rather
    /// than a home that quietly held nothing.
    /// </summary>
    [Fact]
    public void AReadersOwnRefusalIsTheReasonTheHomeIsNamed()
    {
        using var scratch = State();
        scratch.Comparator = "leveldb.SomeOtherComparator";

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Null(diagnosis.Partition);
        Assert.Empty(diagnosis.Ordering.HomesRead);
        Assert.Contains(
            diagnosis.Ordering.HomesNotRead,
            home => home.Reason.Contains("leveldb.SomeOtherComparator", StringComparison.Ordinal));
        Assert.Contains("leveldb.SomeOtherComparator", diagnosis.WhyNoPartition!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A deployment record that is there and unreadable is a caveat, not an
    /// absent record.
    /// </summary>
    [Fact]
    public void ARecordThatCannotBeReadIsSaidSoRatherThanTreatedAsAbsent()
    {
        using var scratch = State();
        File.WriteAllText(
            Path.Combine(gameDirectory, DeploymentRecord.FileName), """{"no":"files"}""");

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Null(diagnosis.Record);
        Assert.Contains(
            diagnosis.Caveats,
            caveat => caveat.Contains("could not be read", StringComparison.Ordinal));
        Assert.Null(diagnosis.Partition);
        Assert.Contains("could not be read", diagnosis.WhyNoPartition!, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "carries no deployment record", diagnosis.WhyNoPartition!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A record no other process will share is refused by name, and takes down
    /// neither the state reading nor the ordering already read.
    /// </summary>
    /// <remarks>
    /// A sharing violation rather than a denial, so the arm is driven on every
    /// platform. Its denied-tier sibling drives the same arm through an ACL.
    /// </remarks>
    [Fact]
    public void ARecordThatCannotBeOpenedIsRefusedRatherThanEscapingRaw()
    {
        using var scratch = State();
        var record = Path.Combine(gameDirectory, DeploymentRecord.FileName);
        File.WriteAllText(record, """{"files":[]}""");

        using var held = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Throws<DiagnosisReadException>(() => DeploymentRecord.In(gameDirectory));

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Null(diagnosis.Record);
        Assert.Null(diagnosis.Partition);
        Assert.Contains("could not be read", diagnosis.WhyNoPartition!, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "carries no deployment record", diagnosis.WhyNoPartition!, StringComparison.Ordinal);

        // The half a raw throw took down with it.
        Assert.NotNull(diagnosis.State);
    }

    [Fact]
    public void TheInUseCaveatTravelsWithTheDiagnosis()
    {
        using var scratch = State();

        Assert.Contains(
            ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory).Caveats,
            caveat => caveat.Contains("whether the manager was running", StringComparison.Ordinal));
    }

    private static string Rule(string kind, string source, string reference) =>
        "{\"type\":\"" + kind + "\","
        + "\"source\":{\"fileMD5\":\"" + source + "\",\"versionMatch\":\"*\"},"
        + "\"reference\":{\"fileMD5\":\"" + reference + "\",\"versionMatch\":\"*\"}}";

    private void Manifest(string rule)
    {
        var directory = Path.Combine(staging, Container);
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, CollectionManifest.FileName),
            """
            {"mods":[
              {"name":"A","source":{"md5":"hash-of-a","logicalFilename":"a.zip","fileId":101}},
              {"name":"B","source":{"md5":"hash-of-b","logicalFilename":"b.zip","fileId":102}}],
             "modRules":[
            """ + rule + "]}");
    }

    private void Record(params (string Path, string Mod)[] files) =>
        File.WriteAllText(
            Path.Combine(gameDirectory, DeploymentRecord.FileName),
            """{"deploymentMethod":"hardlink_activator","gameId":"a-game","targetPath":"x","files":["""
            + string.Join(
                ",",
                files.Select(file =>
                    $$$"""{"relPath":"{{{file.Path}}}","source":"{{{file.Mod}}}"}"""))
            + "]}");

    private SyntheticStateDatabase State(string? stateRule = null)
    {
        var scratch = new SyntheticStateDatabase();

        scratch.Table(
            ($"persistent###profiles###{Profile}###gameId", $"\"{Game}\""),
            ($"persistent###profiles###{Profile}###modState###mod-a###enabled", "true"),
            ($"persistent###profiles###{Profile}###modState###mod-b###enabled", "true"),
            ($"persistent###profiles###{Profile}###modState###{Container}###enabled", "true"),
            ($"persistent###mods###{Game}###mod-a###installationPath", "\"mod-a\""),
            ($"persistent###mods###{Game}###mod-a###type", "\"\""),
            ($"persistent###mods###{Game}###mod-a###attributes###fileMD5", "\"hash-of-a\""),
            ($"persistent###mods###{Game}###mod-a###rules", $"[{stateRule ?? string.Empty}]"),
            ($"persistent###mods###{Game}###mod-b###installationPath", "\"mod-b\""),
            ($"persistent###mods###{Game}###mod-b###type", "\"\""),
            ($"persistent###mods###{Game}###mod-b###attributes###fileMD5", "\"hash-of-b\""),
            ($"persistent###mods###{Game}###{Container}###installationPath", $"\"{Container}\""),
            ($"persistent###mods###{Game}###{Container}###type", "\"collection\""),
            ($"settings###mods###installPath###{Game}", System.Text.Json.JsonSerializer.Serialize(staging)),
            ($"settings###profiles###lastActiveProfile###{Game}", $"\"{Profile}\""));

        return scratch;
    }
}
