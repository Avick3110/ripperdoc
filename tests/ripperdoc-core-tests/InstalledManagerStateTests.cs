using Ripperdoc.Core.Diagnosis;
using Ripperdoc.Core.ManagerState;
using Xunit;
using Xunit.Abstractions;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The state reader over a real manager's own state directory.
/// </summary>
/// <remarks>
/// <para>
/// The subject changes whenever its owner installs a mod or switches a profile,
/// so these assert what holds of any state and <strong>report the numbers
/// rather than asserting them</strong>. A figure pinned here would turn
/// somebody else's manager into a red run.
/// </para>
/// <para>
/// Read-only throughout, and nothing here opens a database or starts the
/// manager. The reader creates nothing in the directory.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.InstalledManagerState)]
public sealed class InstalledManagerStateTests(ITestOutputHelper output)
{
    /// <summary>
    /// The variable naming the state directory, derived from the brand rather
    /// than spelled out, so a rebrand cannot leave a stale name here.
    /// </summary>
    internal static string StateVariableName =>
        Branding.Name.ToUpperInvariant() + "_MANAGER_STATE_PATH";

    /// <summary>The variable naming which game of it to read, derived the same way.</summary>
    internal static string GameVariableName =>
        Branding.Name.ToUpperInvariant() + "_MANAGER_GAME_ID";

    private static string StatePath =>
        Named(StateVariableName, "a deployment manager's own state directory");

    private static string GameId => Named(GameVariableName, "which game of that state to read");

    /// <summary>
    /// The state reads whole, and every figure the characterisation measured is
    /// reported rather than pinned.
    /// </summary>
    [Fact]
    public void TheStateIsReadAndItsOwnFiguresAreReported()
    {
        var reading = Reading();
        var state = reading.State;

        output.WriteLine($"files read          : {string.Join(", ", state.FilesRead)}");
        output.WriteLine($"entries read        : {state.EntriesRead}");
        output.WriteLine($"distinct keys       : {state.KeysSeen}");
        output.WriteLine($"  of those, live    : {state.KeysLive}");
        output.WriteLine($"values materialised : {state.Values.Count}");
        output.WriteLine($"profiles for game   : {reading.ProfileCandidates.Count}");
        output.WriteLine($"profile selected    : {reading.SelectedProfile is not null}");
        output.WriteLine($"mods the manager knows: {reading.Wanted?.Count}");
        output.WriteLine($"  enabled           : {reading.Wanted?.Count(mod => mod.Enabled)}");
        output.WriteLine($"  disabled          : {reading.Wanted?.Count(mod => !mod.Enabled)}");
        output.WriteLine(
            $"installationPath is not the id: {reading.InstallationPathIsNotTheId.Count}");
        output.WriteLine(
            $"installationPath not recorded : {reading.InstallationPathNotRecorded.Count}");
        output.WriteLine($"file spellings naming several mods: "
            + $"{reading.FileSpellingsNamingMoreThanOneMod.Count}");

        Assert.NotEmpty(state.FilesRead);
        Assert.True(state.KeysLive <= state.KeysSeen);
        Assert.True(state.Values.Count <= state.KeysLive);

        Assert.True(
            reading.SelectedProfile is not null || reading.ProfileCandidates.Count == 0,
            $"the state has {reading.ProfileCandidates.Count} profiles for '{reading.GameId}' and "
            + $"names none of them active: {reading.WhyNoProfile}");
    }

    /// <summary>
    /// No value outside the prefixes the reading declares is materialised, so
    /// the namespace holding account credentials is never decoded.
    /// </summary>
    /// <remarks>
    /// The one figure here asserted rather than reported. Which prefixes are
    /// read is the reader's own declaration, and a value outside them is one
    /// this engine held without being asked to.
    /// </remarks>
    [Fact]
    public void NoValueOutsideTheDeclaredPrefixesIsHeld()
    {
        var reading = Reading();
        var prefixes = ManagerStateReading.Prefixes(reading.GameId);

        output.WriteLine($"prefixes declared : {prefixes.Count}");
        output.WriteLine($"keys enumerated   : {reading.State.KeysSeen}");
        output.WriteLine($"values held       : {reading.State.Values.Count}");

        Assert.All(
            reading.State.Values.Keys,
            key => Assert.Contains(
                prefixes,
                prefix => key.StartsWith(prefix, StringComparison.Ordinal)));

        Assert.True(
            reading.State.KeysSeen > reading.State.Values.Count,
            "every key in this state fell under a prefix the reader materialises, so this check "
            + "cannot tell a reader that filters from one that does not");
    }

    /// <summary>
    /// Every rule this reader emits names two mods the manager knows, and every
    /// rule it could not resolve is counted under the kind it declared.
    /// </summary>
    [Fact]
    public void EveryRuleNamesTwoModsTheManagerKnowsAndTheRestAreCounted()
    {
        var reading = Reading();
        var known = reading.Wanted!.Select(mod => mod.Id).ToHashSet(StringComparer.Ordinal);
        var unresolved = reading.RulesNotResolved.Sum(rules => rules.Count);

        output.WriteLine($"rules resolved   : {reading.Rules.Rules.Count}");
        output.WriteLine($"rules unresolved : {unresolved}");
        output.WriteLine($"  by kind        : "
            + string.Join(", ", reading.RulesNotResolved.Select(r => $"{r.DeclaredKind} {r.Count}")));
        output.WriteLine($"rules read       : {reading.Rules.Rules.Count + unresolved}");
        output.WriteLine($"home             : {reading.Rules.Home}");

        Assert.All(reading.Rules.Rules, rule =>
        {
            Assert.Contains(rule.Source, known);
            Assert.Contains(rule.Reference, known);
        });
    }

    /// <summary>
    /// The curated list's manifest is found where the state says it is, and its
    /// rules land on the manager's own identity.
    /// </summary>
    [Fact]
    public void AStagedListsManifestIsFoundFromTheStateAndJoinsToTheManagersIds()
    {
        var reading = Reading();
        var paths = CollectionManifest.PathsIn(reading);
        var known = reading.Wanted!.Select(mod => mod.Id).ToHashSet(StringComparer.Ordinal);

        output.WriteLine($"staging root recorded : {reading.StagingRoot is not null}");
        output.WriteLine($"curated lists staged  : {paths.Count}");

        foreach (var path in paths)
        {
            var manifest = CollectionManifest.In(path, reading);

            output.WriteLine($"  manifest present    : {manifest is not null}");

            if (manifest is null)
            {
                continue;
            }

            output.WriteLine($"  mods declared       : {manifest.DeclaredMods}");
            output.WriteLine($"  not in the state    : {manifest.DeclaredModsNotInTheState}");
            output.WriteLine($"  rules resolved      : {manifest.Rules.Rules.Count}");
            output.WriteLine($"  rules unresolved    : "
                + manifest.RulesNotResolved.Sum(rules => rules.Count));

            Assert.All(manifest.Rules.Rules, rule =>
            {
                Assert.Contains(rule.Source, known);
                Assert.Contains(rule.Reference, known);
            });
        }
    }

    /// <summary>
    /// The graph over both rule homes reports which it read and which it did
    /// not, and its cycles as paths.
    /// </summary>
    [Fact]
    public void TheGraphOverBothHomesNamesWhatItReadAndWhatItDidNot()
    {
        var reading = Reading();
        var read = new List<OrderingRuleSet> { reading.Rules };
        var notRead = new List<UnreadRuleSet>();

        foreach (var path in CollectionManifest.PathsIn(reading))
        {
            var manifest = CollectionManifest.In(path, reading);

            if (manifest is null)
            {
                notRead.Add(new UnreadRuleSet(path, "the manager stages a list here with no manifest in it"));
            }
            else
            {
                read.Add(manifest.Rules);
            }
        }

        var graph = OrderingGraph.Over(read, notRead);

        output.WriteLine($"homes read     : {read.Count}");
        output.WriteLine($"homes not read : {notRead.Count}");
        output.WriteLine($"nodes          : {graph.NodeCount}");
        output.WriteLine($"edges          : {graph.EdgeCount}");
        output.WriteLine($"rules not edged: "
            + string.Join(", ", graph.RulesNotEdges.Select(r => $"{r.Kind} {r.Count}")));
        output.WriteLine($"cycles         : {graph.Cycles.Count}");

        foreach (var cycle in graph.Cycles.Take(3))
        {
            output.WriteLine("  " + string.Join(" -> ", cycle.Path));
        }

        Assert.Equal(read.Count, graph.HomesRead.Count);
        Assert.Equal(notRead.Count, graph.HomesNotRead.Count);
    }

    /// <summary>
    /// A state and a record from different deployments, partitioned - and
    /// labelled as that rather than reported as this bench's partition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identity law is what makes the mismatch visible instead of silent: a
    /// record written for another deployment names mods this state does not
    /// know, and they come out as unclaimed rather than being absorbed.
    /// </para>
    /// <para>
    /// <strong>This is a measurement of that visibility, not of the bench.</strong>
    /// Which pair it ran on is printed, and nothing here says the two belong
    /// together.
    /// </para>
    /// </remarks>
    [Fact]
    public void APairFromDifferentDeploymentsShowsAsUnclaimedRatherThanAsAgreement()
    {
        var recordPath = Environment.GetEnvironmentVariable(
            InstalledManagerLaneTests.RecordVariableName);

        if (string.IsNullOrWhiteSpace(recordPath) || !File.Exists(recordPath))
        {
            output.WriteLine(
                $"SKIPPED - {InstalledManagerLaneTests.RecordVariableName} names no readable "
                + "record, so there is no deployed side to pair this state against");

            return;
        }

        var reading = Reading();
        var record = DeploymentRecord.Parse(File.ReadAllText(recordPath), recordPath);
        var partition = DeploymentPartition.Of(reading.Wanted!, record);

        output.WriteLine($"state directory : {StatePath}");
        output.WriteLine($"record          : {recordPath}");
        output.WriteLine($"wanted          : {reading.Wanted!.Count(mod => mod.Enabled)}");
        output.WriteLine($"record mods     : "
            + record.Files.Select(file => file.SourceMod).Distinct(StringComparer.Ordinal).Count());

        foreach (var bucket in Enum.GetValues<PartitionBucket>())
        {
            output.WriteLine($"  {bucket,-13}: {partition.Count(bucket)}");
        }

        Assert.Equal(partition.Mods.Count, Enum.GetValues<PartitionBucket>().Sum(partition.Count));
        Assert.All(partition.Mods, mod => Assert.False(string.IsNullOrWhiteSpace(mod.Reason)));
        Assert.True(partition.RecordWasRead);
    }

    private static ManagerStateReading Reading() =>
        ManagerStateReading.Of(StatePath, GameId)
        ?? throw new InvalidOperationException(
            $"{StateVariableName} names '{StatePath}', which holds no state database - there is "
            + $"no {StateVersion.PointerName} in it. Point it at a manager's own state directory, "
            + "or at a verified copy of one.");

    /// <remarks>
    /// The gate script's skips are written from outside; this is the same
    /// announcement from inside, for the runs that do not come through it.
    /// </remarks>
    private static string Named(string variable, string what)
    {
        var value = Environment.GetEnvironmentVariable(variable);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"The {TierTrait.InstalledManagerState} checks read {what}, which no runner has. "
                + $"Set {variable} to one to run them. This tier needs both of its inputs: "
                + $"{StateVariableName} and {GameVariableName}. The gate script announces the "
                + "tier as skipped, by name, when it cannot run it - an absent input is never "
                + "reported as a pass.")
            : value;
    }
}
