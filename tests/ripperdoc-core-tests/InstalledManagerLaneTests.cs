using Ripperdoc.Core.Diagnosis;
using Xunit;
using Xunit.Abstractions;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The diagnosis lane over a real deployment record and a real compiler log.
/// </summary>
/// <remarks>
/// <para>
/// The subject changes whenever its owner deploys, so these assert what holds of
/// any record and any log and <strong>report the numbers rather than asserting
/// them</strong>. A figure pinned here would turn somebody else's install into a
/// red run.
/// </para>
/// <para>
/// Both inputs are read and neither is written. Nothing here starts the manager
/// or the game.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.InstalledManagerLane)]
public sealed class InstalledManagerLaneTests(ITestOutputHelper output)
{
    private static readonly string? RecordPath =
        Environment.GetEnvironmentVariable("RIPPERDOC_DEPLOYMENT_RECORD_PATH");

    private static readonly string? LogPath =
        Environment.GetEnvironmentVariable("RIPPERDOC_COMPILER_LOG_PATH");

    /// <summary>
    /// The record parses, and every entry it holds carries both a path and a
    /// mod.
    /// </summary>
    /// <remarks>
    /// True of any record by the reader's own refusal, so what this adds is that
    /// a real one goes through it at all - the shapes the reader refuses were
    /// chosen from one manager's output and a second manager version writing
    /// something else would surface here rather than in a diagnosis.
    /// </remarks>
    [Fact]
    public void TheRecordIsReadAndItsEntriesAreWholeAsRead()
    {
        var record = Record();

        output.WriteLine($"deployment method : {record.Method}");
        output.WriteLine($"files claimed     : {record.Files.Count}");
        output.WriteLine($"distinct mods     : {Mods(record).Count}");

        Assert.NotEmpty(record.Files);
        Assert.All(record.Files, file =>
        {
            Assert.False(string.IsNullOrWhiteSpace(file.RelativePath));
            Assert.False(string.IsNullOrWhiteSpace(file.SourceMod));
        });
    }

    /// <summary>
    /// A real compiler log is placed at a boot by its own contents.
    /// </summary>
    /// <remarks>
    /// The one figure here that is asserted rather than reported: a compiler log
    /// this engine cannot place is a grammar this engine does not have, and that
    /// is a gap to surface rather than a number to print.
    /// </remarks>
    [Fact]
    public void TheCompilerLogIsPlacedByItsOwnContents()
    {
        var log = LogAttribution.Of(LogPath!);

        output.WriteLine($"log             : {log.FileName}");
        output.WriteLine($"grammar         : {log.Grammar}");
        output.WriteLine($"instant         : {log.Instant:O}");

        Assert.True(
            log.IsAttributed,
            $"'{log.FileName}' yielded no timestamp under any declared grammar, so this engine "
            + "cannot say which boot it records. That is a missing grammar, not a property of "
            + "the log.");
    }

    /// <summary>
    /// Every error the log reports is accounted for exactly once - attributed to
    /// a mod, or named as unclaimed, or named as outside the game directory.
    /// </summary>
    /// <remarks>
    /// The exhaustiveness claim over real input. Its numbers are reported; what
    /// is asserted is that the three outcomes cover every source the log names,
    /// because an error falling out of all three is the silent bucket the lane
    /// exists to refuse.
    /// </remarks>
    [Fact]
    public void EverySourceTheLogNamesIsAccountedForExactlyOnce()
    {
        var record = Record();
        var reading = CompileFailureReading.Of(LogPath!, record, record.TargetPath);

        var attributed = reading.Suspects.SelectMany(suspect => suspect.Errors)
            .Select(error => error.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var named = reading.Errors.Select(error => error.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        output.WriteLine($"errors read           : {reading.Errors.Count}");
        output.WriteLine($"error lines not read  : {reading.ErrorLinesNotRead}");
        output.WriteLine($"distinct sources      : {named.Count}");
        output.WriteLine($"  attributed          : {attributed.Count}");
        output.WriteLine($"  unclaimed by record : {reading.SourcesTheRecordDoesNotClaim.Count}");
        output.WriteLine($"  outside game dir    : {reading.SourcesOutsideTheGameDirectory.Count}");
        output.WriteLine($"mods implicated       : {reading.Suspects.Count}");

        Assert.Equal(
            named.Count,
            attributed.Count
            + reading.SourcesTheRecordDoesNotClaim.Count
            + reading.SourcesOutsideTheGameDirectory.Count);

        Assert.Equal(
            reading.Errors.Count,
            reading.Suspects.Sum(suspect => suspect.Errors.Count)
            + reading.Errors.Count(error => !attributed.Contains(error.SourcePath)));
    }

    /// <summary>
    /// The partition over the record's own deployed side is exhaustive, with a
    /// reason on every mod.
    /// </summary>
    /// <remarks>
    /// The wanted side is taken from the record itself here rather than from the
    /// manager's state, so what this exercises is the partition's exhaustiveness
    /// against a real deployed set of real size. Reading the wanted set from the
    /// manager is a separate input this tier does not have.
    /// </remarks>
    [Fact]
    public void ThePartitionOverTheRecordsOwnModsIsExhaustive()
    {
        var record = Record();
        var known = Mods(record)
            .Select(id => new ManagerMod(id, Enabled: true, Kind: string.Empty))
            .ToList();

        var partition = DeploymentPartition.Of(known, record);

        output.WriteLine($"mods partitioned : {partition.Mods.Count}");
        foreach (var bucket in Enum.GetValues<PartitionBucket>())
        {
            output.WriteLine($"  {bucket,-13}: {partition.Count(bucket)}");
        }

        Assert.Equal(known.Count, partition.Mods.Count);
        Assert.Equal(partition.Mods.Count, Enum.GetValues<PartitionBucket>().Sum(partition.Count));
        Assert.All(partition.Mods, mod => Assert.False(string.IsNullOrWhiteSpace(mod.Reason)));
    }

    private static DeploymentRecord Record() =>
        DeploymentRecord.Parse(File.ReadAllText(RecordPath!), RecordPath!);

    private static IReadOnlyList<string> Mods(DeploymentRecord record) =>
        [.. record.Files.Select(file => file.SourceMod).Distinct(StringComparer.Ordinal)];
}
