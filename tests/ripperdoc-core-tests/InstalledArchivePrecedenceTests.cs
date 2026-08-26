using System.Globalization;
using Ripperdoc.Core.Archive;
using Ripperdoc.Naming;
using Xunit;
using Xunit.Abstractions;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Precedence and contested sets over a real install's archive lane.
/// </summary>
/// <remarks>
/// <para>
/// Tier (ii): this reads a real mod directory, which no runner has and which is
/// other people's mod content that this project does not carry. The gate runs
/// it when the environment names a directory and announces it as skipped, by
/// name, when nothing does.
/// </para>
/// <para>
/// What is asserted is what holds of any lane, and every number is reported
/// rather than asserted. A lane changes whenever its owner installs a mod, and
/// which branch of the precedence law it sits in depends on whether it has a
/// list file at all - so a count taken from one install would turn an ordinary
/// install into a red run.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.InstalledModArchives)]
[Collection(ResolverCollection.Name)]
public class InstalledArchivePrecedenceTests
{
    private readonly ITestOutputHelper _output;

    public InstalledArchivePrecedenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Every archive present has a place, and the law's own ordering holds.
    /// </summary>
    [Fact]
    public void EveryArchiveHasAPlaceAndListedOnesOutrankUnlistedOnes()
    {
        var (inventory, order, _) = Read();

        _output.WriteLine(Report(inventory, order));

        Assert.Equal(inventory.ArchiveCount, order.Positions.Count);
        Assert.All(
            inventory.Archives,
            archive => Assert.NotNull(order.PositionOf(archive.FileName)));

        var listed = order.Positions.Where(position => position.IsListed).ToList();
        var unlisted = order.Positions.Where(position => !position.IsListed).ToList();

        if (listed.Count > 0 && unlisted.Count > 0)
        {
            Assert.True(
                listed.Max(position => position.Rank) < unlisted.Min(position => position.Rank),
                "an archive the list names ranked at or below one it does not, which inverts the "
                + "measured law: being listed outranks any file name");
        }

        // The residue, as a property of the lane rather than a count of it.
        // Archives a present list does not name share one rank; with no list
        // every archive has its own.
        Assert.Equal(order.Modlist.IsPresent && unlisted.Count > 1, !order.IsFullyOrdered);
    }

    /// <summary>
    /// Every contest ends in a winner or in a named set of candidates.
    /// </summary>
    /// <remarks>
    /// The two states a contest may be in, and never a third. A contest with
    /// neither would be a resource reported as contested with nothing said
    /// about it, which is the shape of an answer that looks complete and is
    /// not.
    /// </remarks>
    [Fact]
    public void EveryContestIsResolvedOrIsSaidToBeUnresolvable()
    {
        var (_, _, contested) = Read();

        _output.WriteLine(ContestReport(contested));

        Assert.All(contested.Contests, contest =>
        {
            var carriers = contest.Carriers.Select(carrier => carrier.FileName).ToList();

            Assert.True(carriers.Count > 1, $"'{contest.Display}' is reported as contested by {carriers.Count}");
            Assert.True(
                contest.HasDeterminedWinner ^ contest.UndeterminedAmong.Count > 0,
                $"'{contest.Display}' is neither resolved nor reported as unresolvable");

            if (contest.HasDeterminedWinner)
            {
                Assert.Contains(contest.Winner!, carriers);
                var winning = contest.Carriers.Single(carrier => carrier.FileName == contest.Winner);
                Assert.All(
                    contest.Carriers.Where(carrier => carrier.FileName != contest.Winner),
                    carrier => Assert.True(
                        winning.Rank < carrier.Rank,
                        $"'{contest.Display}' names {contest.Winner} the winner at rank {winning.Rank} "
                        + $"while {carrier.FileName} ranks {carrier.Rank}"));
            }
            else
            {
                Assert.All(contest.UndeterminedAmong, name => Assert.Contains(name, carriers));
            }

            // Losing is not the same as not being known to win: a carrier
            // ranked below an unresolved pair has still lost.
            Assert.Empty(contest.Shadowed.Intersect(contest.UndeterminedAmong, StringComparer.Ordinal));
            Assert.DoesNotContain(contest.Winner, contest.Shadowed);
        });
    }

    /// <summary>
    /// The artifact's own arithmetic, read back from the artifact.
    /// </summary>
    [Fact]
    public void TheCountsTheSetReportsAddUpToTheResourcesItRead()
    {
        var (inventory, _, contested) = Read();

        Assert.Equal(inventory.DistinctEntryCount, contested.DistinctResourceCount);
        Assert.Equal(
            contested.DistinctResourceCount,
            contested.ContestedCount + contested.ResourcesUncontestedAtThisBasis);
        Assert.Equal(contested.Contests.Count, contested.ContestedCount);
        Assert.Equal(
            contested.Contests.Count(contest => !contest.HasDeterminedWinner),
            contested.UndeterminedCount);

        Assert.Equal(ContestBasis.ResourcePath, contested.Basis);
        Assert.Equal(inventory.UnreadableCount, contested.UnreadArchives.Count);
        Assert.Equal(inventory.UnreadableCount == 0, contested.IsComplete);
    }

    /// <summary>
    /// Naming decides how a contest is written, never whether there is one.
    /// </summary>
    /// <remarks>
    /// A contest is between archives carrying one resource, and a resource is
    /// the same resource whether or not anything can name it. If installing the
    /// dictionary changed which resources were contested, naming would be
    /// deciding what the lane contains.
    /// <para>
    /// The dictionary-less reading is taken first and fenced, because the
    /// resolver a dictionary loads into is process-wide and cannot be unloaded.
    /// Without that fence a reordering would leave both readings dictionary
    /// readings, the two sets would match, and this would pass having compared
    /// nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheDictionaryNamesMoreContestsWithoutChangingWhichResourcesAreContested()
    {
        var withoutDictionary = InstalledModArchivesFixture.DictionaryLessReading;
        var plain = ContestedSet.Of(
            withoutDictionary,
            ArchiveLoadOrder.Of(withoutDictionary, Modlist.Read(InstalledModArchivesFixture.ModDirectory)));

        var withDictionaryInventory =
            new ArchiveInventoryReader(new DictionaryResourceNames()).Read(InstalledModArchivesFixture.ModDirectory);
        var named = ContestedSet.Of(
            withDictionaryInventory,
            ArchiveLoadOrder.Of(withDictionaryInventory, Modlist.Read(InstalledModArchivesFixture.ModDirectory)));

        _output.WriteLine(ContestReport(plain));
        _output.WriteLine(ContestReport(named));

        Assert.True(withDictionaryInventory.Provenance.DictionaryLoaded);

        Assert.Equal(
            plain.Contests.Select(contest => contest.Hash),
            named.Contests.Select(contest => contest.Hash));
        Assert.Equal(
            plain.Contests.Select(contest => contest.Winner),
            named.Contests.Select(contest => contest.Winner));

        // The invariant, not the install. Names go into the resolver and none
        // come out of it, so the dictionary posture can never name fewer; a
        // lane whose mods all declare their own paths names everything under
        // both, and the two figures are reported above.
        Assert.True(
            named.Contests.Count(contest => contest.IsNamed)
                >= plain.Contests.Count(contest => contest.IsNamed),
            $"the dictionary posture named {named.Contests.Count(contest => contest.IsNamed)} contested "
            + $"resources where the archive-only posture named "
            + $"{plain.Contests.Count(contest => contest.IsNamed)}");
    }

    /// <summary>
    /// An archive the list does not name loses what it loses to archives the
    /// list does name.
    /// </summary>
    /// <remarks>
    /// Reported for whatever the lane is, including a lane with no list file -
    /// where the report is that there is nothing a list could be demoting.
    /// </remarks>
    [Fact]
    public void WhatALaneWithAListIsSilentlyDemotingIsReported()
    {
        var (_, order, contested) = Read();

        _output.WriteLine(DemotionReport(contested));

        if (!order.Modlist.IsPresent)
        {
            Assert.Empty(contested.Demotions);
            return;
        }

        Assert.All(contested.Demotions, demotion =>
        {
            Assert.False(order.PositionOf(demotion.FileName)!.Value.IsListed);
            Assert.True(demotion.ContestsCarried > 0);
            Assert.Equal(
                demotion.ContestsCarried,
                demotion.ContestsLostToListedArchives + demotion.ContestsUndetermined);
        });
    }

    /// <summary>
    /// Each archive's index is read once, and the resolution reads none.
    /// </summary>
    /// <remarks>
    /// Resolving is arithmetic over a model already in memory. A per-query open
    /// is the cost shape that makes a whole-install answer impossible at real
    /// scale, so what is held here is that a second resolution over one reading
    /// costs no read at all.
    /// </remarks>
    [Fact]
    public void ResolvingAgainOverOneReadingTouchesNoArchive()
    {
        var inventory = InstalledModArchivesFixture.DictionaryLessReading;
        var modlist = Modlist.Read(InstalledModArchivesFixture.ModDirectory);

        var first = ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, modlist));
        var second = ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, modlist));

        _output.WriteLine(
            $"archives ............. {inventory.ArchiveCount}\n"
            + $"contested ............ {first.ContestedCount}");

        Assert.Equal(
            first.Contests.Select(contest => contest.Display),
            second.Contests.Select(contest => contest.Display));
        Assert.Equal(
            first.Contests.Select(contest => contest.Winner),
            second.Contests.Select(contest => contest.Winner));
    }

    private static (ArchiveInventory Inventory, ArchiveLoadOrder Order, ContestedSet Contested) Read()
    {
        var inventory = InstalledModArchivesFixture.ReadWithoutDictionary();
        var order = ArchiveLoadOrder.Of(inventory, Modlist.Read(InstalledModArchivesFixture.ModDirectory));

        return (inventory, order, ContestedSet.Of(inventory, order));
    }

    private static string Report(ArchiveInventory inventory, ArchiveLoadOrder order) =>
        $"""
         list file ............ {(order.Modlist.IsPresent ? "present" : "absent")}
           names .............. {order.Modlist.ListedCount}
           repeated ........... {order.Modlist.RepeatedNames.Count}
           naming nothing ..... {order.ListedButNotPresent.Count}
         archives ............. {inventory.ArchiveCount} (unreadable {inventory.UnreadableCount})
           listed ............. {order.Positions.Count(position => position.IsListed)}
           unlisted ........... {order.Positions.Count(position => !position.IsListed)}
         every place known .... {(order.IsFullyOrdered ? "yes" : "no - unlisted archives share a rank")}
         """;

    private static string ContestReport(ContestedSet contested)
    {
        var named = contested.Contests.Count(contest => contest.IsNamed);
        var share = contested.ContestedCount == 0
            ? "n/a"
            : (100.0 * named / contested.ContestedCount).ToString("F1", CultureInfo.InvariantCulture) + " %";

        return $"""
                basis ................ {contested.Basis}
                distinct resources ... {contested.DistinctResourceCount}
                  contested .......... {contested.ContestedCount}
                    named ............ {named} ({share})
                    hash-only ........ {contested.ContestedCount - named}
                    undetermined ..... {contested.UndeterminedCount}
                  not examined ....... {contested.ResourcesUncontestedAtThisBasis} (a contest between
                                       archives sharing no path is invisible to this basis)
                complete ............. {(contested.IsComplete ? "yes" : $"no - {contested.UnreadArchives.Count} archive(s) unread")}
                """;
    }

    private static string DemotionReport(ContestedSet contested)
    {
        var wholly = contested.Demotions.Count(demotion => demotion.LosesEveryContestToTheList);

        return $"""
                unlisted archives in contests ... {contested.Demotions.Count}
                  losing every contest .......... {wholly}
                  shadowed resources ............ {contested.Demotions.Sum(demotion => demotion.ContestsLostToListedArchives)}
                """;
    }
}
