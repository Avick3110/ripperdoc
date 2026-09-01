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
    /// <summary>
    /// The variable naming the deployment record, derived from the brand rather
    /// than spelled out, so a rebrand cannot leave a stale name here.
    /// </summary>
    internal static string RecordVariableName =>
        Branding.Name.ToUpperInvariant() + "_DEPLOYMENT_RECORD_PATH";

    /// <summary>The variable naming the compiler log, derived the same way.</summary>
    internal static string LogVariableName =>
        Branding.Name.ToUpperInvariant() + "_COMPILER_LOG_PATH";

    private static string RecordPath =>
        Named(RecordVariableName, "a deployment manager's own record of what it deployed");

    private static string LogPath =>
        Named(LogVariableName, "a compiler log from a boot that ran against that deployment");

    /// <summary>
    /// The record parses, and every entry it holds carries both a path and a
    /// mod.
    /// </summary>
    [Fact]
    public void TheRecordIsReadAndItsEntriesAreWholeAsRead()
    {
        var record = Record();

        output.WriteLine($"deployment method : {record.Method}");
        output.WriteLine($"files claimed     : {record.Files.Count}");
        output.WriteLine($"distinct mods     : {Mods(record).Count}");

        Assert.NotEmpty(record.Files);
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
        var log = LogAttribution.Of(LogPath);

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
    [Fact]
    public void EverySourceTheLogNamesIsAccountedForExactlyOnce()
    {
        var record = Record();
        var reading = CompileFailureReading.Of(LogPath, record, record.TargetPath);

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

        Assert.False(
            named.Count > 0 && reading.SourcesOutsideTheGameDirectory.Count == named.Count,
            $"every one of the {named.Count} sources this log names sits outside the directory "
            + "the record deployed into, so this pair attributes nothing. Either the record and "
            + "the log describe different deployments, or the record's target path is not the "
            + "one the compiler wrote its paths against - a gap to surface, not a number to "
            + "print");

        Assert.False(
            named.Count > 0 && reading.SourcesTheRecordDoesNotClaim.Count == named.Count,
            $"every one of the {named.Count} sources this log names sits under the directory the "
            + "record deployed into and is claimed by no entry in it, so this pair attributes "
            + "nothing. Either the record and the log describe different deployments, or the "
            + "record spells its entries in a way this join does not resolve against the paths "
            + "the compiler wrote - a gap to surface, not a number to print");
    }

    /// <summary>
    /// The partition over the record's own deployed side is exhaustive, with a
    /// reason on every mod.
    /// </summary>
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

        Assert.True(partition.RecordWasRead);
        Assert.Equal(known.Count, partition.Count(PartitionBucket.Deployed));
        Assert.Equal(0, partition.Count(PartitionBucket.Missing));
        Assert.Equal(0, partition.Count(PartitionBucket.Unresolvable));
        Assert.Equal(0, partition.Count(PartitionBucket.Unclaimed));
    }

    /// <summary>
    /// The path a variable names, or a refusal that names this tier and both of
    /// its inputs.
    /// </summary>
    /// <remarks>
    /// The gate script's skips are written from outside; this is the same
    /// announcement from inside, for the runs that do not come through it. Both
    /// variables are named whichever one is missing, because the tier needs the
    /// pair - a record on its own attributes nothing, and a log read against a
    /// different deployment attributes to whatever that one held.
    /// </remarks>
    private static string Named(string variable, string what)
    {
        var path = Environment.GetEnvironmentVariable(variable);

        return string.IsNullOrWhiteSpace(path)
            ? throw new InvalidOperationException(
                $"The {TierTrait.InstalledManagerLane} checks read {what}, which no runner has. "
                + $"Set {variable} to one to run them. This tier needs both of its inputs: "
                + $"{RecordVariableName} and {LogVariableName}. The gate script announces the "
                + "tier as skipped, by name, when it cannot run it - an absent input is never "
                + "reported as a pass.")
            : path;
    }

    private static DeploymentRecord Record() =>
        DeploymentRecord.Parse(File.ReadAllText(RecordPath), RecordPath);

    private static IReadOnlyList<string> Mods(DeploymentRecord record) =>
        [.. record.Files.Select(file => file.SourceMod).Distinct(StringComparer.Ordinal)];
}
