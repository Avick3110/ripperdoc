using System.Reflection;
using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Which archive wins a contested resource, and what the answer says about
/// itself.
/// </summary>
/// <remarks>
/// Built from inventories rather than from written archives. An archive this
/// project authors carries its own paths, so it cannot express a contested
/// resource nothing can name - and a real lane is full of them. The law's own
/// decision tables are reproduced over real containers, in
/// <see cref="ArchiveLoadOrderTests" />.
/// </remarks>
public sealed class ContestedSetTests
{
    private const string ContestedPath = @"base\rdp\contested.json";

    [Fact]
    public void AResourceOnlyOneArchiveCarriesIsNoContest()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (2, @"base\rdp\other.json")));

        Assert.Empty(contested.Contests);
        Assert.Equal(2, contested.DistinctResourceCount);
        Assert.Equal(2, contested.ResourcesUncontestedAtThisBasis);
    }

    [Fact]
    public void TheCarrierThatLoadsFirstWinsAndTheRestAreShadowed()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)),
            Carrying("rdp_c.archive", (1, ContestedPath)));

        var contest = Assert.Single(contested.Contests);

        Assert.Equal("rdp_a.archive", contest.Winner);
        Assert.Equal(new[] { "rdp_b.archive", "rdp_c.archive" }, contest.Shadowed);
        Assert.Empty(contest.UndeterminedAmong);
        Assert.Equal(
            new[] { "rdp_a.archive", "rdp_b.archive", "rdp_c.archive" },
            contest.Carriers.Select(carrier => carrier.FileName));
    }

    /// <summary>
    /// A contest the law does not decide gets no winner.
    /// </summary>
    /// <remarks>
    /// The order among archives a present list does not name was never
    /// measured, so a contest whose leading carriers are all unlisted has a
    /// winner this project cannot name. Naming one anyway would be a confident
    /// answer to a question nothing has answered - and it would be right half
    /// the time, which is the worst rate for a claim nobody re-checks.
    /// </remarks>
    [Fact]
    public void AContestBetweenTwoUnlistedArchivesHasNoWinnerAndSaysWhichTheyAre()
    {
        var contested = Resolve(
            modlist: ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (9, @"base\rdp\listed.json")),
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        var contest = Assert.Single(contested.Contests);

        Assert.Null(contest.Winner);
        Assert.False(contest.HasDeterminedWinner);
        Assert.Equal(new[] { "rdp_a.archive", "rdp_b.archive" }, contest.UndeterminedAmong);
        Assert.Empty(contest.Shadowed);
        Assert.Equal(1, contested.UndeterminedCount);
    }

    /// <summary>
    /// An archive below an unresolved pair has still lost.
    /// </summary>
    /// <remarks>
    /// Not knowing which of two archives wins is a different thing from not
    /// knowing whether a third one does. The third is ranked below both, so its
    /// version is out either way, and reporting it as undetermined would spend
    /// a caller's attention on a question that is settled.
    /// </remarks>
    [Fact]
    public void ACarrierRankedBelowAnUnresolvedPairIsShadowedRatherThanUndetermined()
    {
        // The listed archive carries a different resource here, so the contest
        // is between the two unlisted ones only; below, it carries the
        // contested one and outranks them both.
        var contested = Resolve(
            modlist: ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (9, @"base\rdp\listed.json")),
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        var withListedCarrier = Resolve(
            modlist: ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (1, ContestedPath)),
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        Assert.Empty(Assert.Single(contested.Contests).Shadowed);

        var contest = Assert.Single(withListedCarrier.Contests);
        Assert.Equal("rdp_listed.archive", contest.Winner);
        Assert.Equal(new[] { "rdp_a.archive", "rdp_b.archive" }, contest.Shadowed);
        Assert.Empty(contest.UndeterminedAmong);
    }

    /// <summary>
    /// An archive losing every contest to the list is reported as such.
    /// </summary>
    /// <remarks>
    /// The state the measurement calls out as invisible: the archive loads, it
    /// works, and it contributes nothing to anything it shares, because it is
    /// not on a list. Resolving the winners correctly still leaves that
    /// unsaid, so it is computed and named.
    /// </remarks>
    [Fact]
    public void AnUnlistedArchiveThatLosesEveryContestToTheListIsNamed()
    {
        var contested = Resolve(
            modlist: ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (1, ContestedPath), (2, @"base\rdp\second.json")),
            Carrying("rdp_newcomer.archive", (1, ContestedPath), (2, @"base\rdp\second.json")));

        var demotion = Assert.Single(contested.Demotions);

        Assert.Equal("rdp_newcomer.archive", demotion.FileName);
        Assert.Equal(2, demotion.ContestsCarried);
        Assert.Equal(2, demotion.ContestsLostToListedArchives);
        Assert.Equal(0, demotion.ContestsUndetermined);
        Assert.True(demotion.LosesEveryContestToTheList);
    }

    [Fact]
    public void AnUnlistedArchiveThatLosesOneContestAndTiesAnotherIsNotLosingEveryContest()
    {
        var contested = Resolve(
            modlist: ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (1, ContestedPath)),
            Carrying("rdp_newcomer.archive", (1, ContestedPath), (2, @"base\rdp\second.json")),
            Carrying("rdp_other.archive", (2, @"base\rdp\second.json")));

        var demotion = Assert.Single(
            contested.Demotions, entry => entry.FileName == "rdp_newcomer.archive");

        Assert.Equal(2, demotion.ContestsCarried);
        Assert.Equal(1, demotion.ContestsLostToListedArchives);
        Assert.Equal(1, demotion.ContestsUndetermined);
        Assert.False(demotion.LosesEveryContestToTheList);
    }

    /// <summary>
    /// With no list file there is nothing to be demoted by.
    /// </summary>
    /// <remarks>
    /// Precedence then follows file names, which a reader can see and a rename
    /// can change. The hazard this reports is the one a rename cannot fix.
    /// </remarks>
    [Fact]
    public void WithNoListFileNoArchiveIsReportedAsDemoted()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        Assert.Empty(contested.Demotions);
        Assert.Equal("rdp_a.archive", Assert.Single(contested.Contests).Winner);
    }

    /// <summary>
    /// A set computed over archives that could not all be read says so.
    /// </summary>
    /// <remarks>
    /// An archive nothing could read may carry any of these resources, and
    /// where it ranks first it wins one. So a winner named here can be wrong,
    /// and the artifact has to carry that rather than present itself as the
    /// whole directory.
    /// </remarks>
    [Fact]
    public void AnArchiveThatCouldNotBeReadLeavesTheSetIncompleteAndNamed()
    {
        var inventory = new ArchiveInventory(
            [
                ArchiveContents.Read("rdp_a.archive", [new ArchiveEntry(1, ContestedPath, 1, 1)]),
                ArchiveContents.Read("rdp_b.archive", [new ArchiveEntry(1, ContestedPath, 1, 1)]),
                ArchiveContents.Unreadable("rdp_broken.archive", ArchiveFailureKind.MalformedContainer, "it raised"),
            ],
            [],
            default);

        var contested = ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, Modlist.Absent));

        Assert.False(contested.IsComplete);
        Assert.Equal(new[] { "rdp_broken.archive" }, contested.UnreadArchives);

        // The archives that were read still resolve. An unreadable one costs
        // its own contents, not everyone else's answer.
        Assert.Equal("rdp_a.archive", Assert.Single(contested.Contests).Winner);
    }

    /// <summary>
    /// An archive in a subdirectory neither joins the contests nor spoils the
    /// completeness of the ones there are.
    /// </summary>
    /// <remarks>
    /// Whether the game loads one is not measured, so it is not ordered and not
    /// resolved. Counting it as unread would report the mod directory's own
    /// answer as partial on account of a file the answer never claimed to
    /// cover.
    /// </remarks>
    [Fact]
    public void AnArchiveInASubdirectoryIsNeitherResolvedNorCountedAgainstTheSet()
    {
        var inventory = new ArchiveInventory(
            [
                ArchiveContents.Read("rdp_a.archive", [new ArchiveEntry(1, ContestedPath, 1, 1)]),
                ArchiveContents.Read("rdp_b.archive", [new ArchiveEntry(1, ContestedPath, 1, 1)]),
            ],
            [@"nested\rdp_hidden.archive"],
            default);

        var contested = ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, Modlist.Absent));

        Assert.True(contested.IsComplete);
        Assert.Empty(contested.UnreadArchives);
        Assert.Equal(2, contested.Order.Positions.Count);
        Assert.DoesNotContain(
            @"nested\rdp_hidden.archive",
            Assert.Single(contested.Contests).Carriers.Select(carrier => carrier.FileName));
    }

    /// <summary>
    /// A contested resource nothing can name is reported by hash.
    /// </summary>
    /// <remarks>
    /// The naming posture's whole point, carried into the artifact the archive
    /// layer exists to produce. A contested set that dropped its nameless
    /// entries would report fewer contests than there are and call the number
    /// complete.
    /// </remarks>
    [Fact]
    public void AContestedResourceWithNoNameIsReportedByHash()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (4242, null)),
            Carrying("rdp_b.archive", (4242, null)));

        var contest = Assert.Single(contested.Contests);

        Assert.False(contest.IsNamed);
        Assert.Equal("4242", contest.Display);
        Assert.Equal("rdp_a.archive", contest.Winner);
    }

    [Fact]
    public void AResourceNamedByOneCarrierAndNamelessInAnotherIsNamed()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (4242, null)),
            Carrying("rdp_b.archive", (4242, ContestedPath)));

        var contest = Assert.Single(contested.Contests);

        Assert.True(contest.IsNamed);
        Assert.Equal(ContestedPath, contest.Name);
    }

    /// <summary>
    /// Both sites that write a resource call the one home of that rule.
    /// </summary>
    /// <remarks>
    /// Identity, not equal output. The defect this guards is a site that grew
    /// a private copy of "report by hash, never omit", and a copy producing
    /// the same string today is invisible to any comparison of what the two
    /// produce - it diverges on the day the shared rule changes and not
    /// before. What is asserted is that the shared method is the one each site
    /// calls.
    /// </remarks>
    [Theory]
    [InlineData(typeof(ArchiveEntry))]
    [InlineData(typeof(ContestedResource))]
    public void EachSiteThatWritesAResourceCallsTheOneHomeOfTheRule(Type site)
    {
        var callees = CalleesOf(site.GetProperty(nameof(ArchiveEntry.Display))!.GetGetMethod()!);

        Assert.Contains(
            callees,
            callee => callee.DeclaringType == typeof(ResourceDisplay)
                && callee.Name == nameof(ResourceDisplay.Of));
    }

    /// <summary>
    /// Every method a method calls, as its own body names them.
    /// </summary>
    /// <remarks>
    /// A scan for call opcodes rather than a decode of every instruction, so
    /// four bytes of another instruction's operand can look like a token. That
    /// direction is harmless here: a stray one resolves to nothing or to
    /// something irrelevant, while a call that is really made always leaves a
    /// token that resolves.
    /// </remarks>
    private static IReadOnlyList<MethodBase> CalleesOf(MethodInfo method)
    {
        var body = method.GetMethodBody()!.GetILAsByteArray()!;
        var callees = new List<MethodBase>();

        for (var index = 0; index + 4 < body.Length; index++)
        {
            // call and callvirt, each carrying a four-byte method token.
            if (body[index] is not (0x28 or 0x6F))
            {
                continue;
            }

            try
            {
                if (method.Module.ResolveMethod(BitConverter.ToInt32(body, index + 1)) is { } callee)
                {
                    callees.Add(callee);
                }
            }
            catch (ArgumentException)
            {
                // Not a method token.
            }
        }

        return callees;
    }

    /// <summary>
    /// The rule itself, at both sites.
    /// </summary>
    /// <remarks>
    /// What each site produces for a hash with a name, without one, and for
    /// the empty name. The binding between them is asserted separately,
    /// because equal output cannot establish it.
    /// </remarks>
    [Theory]
    [InlineData(7UL, null)]
    [InlineData(7UL, ContestedPath)]
    [InlineData(0UL, "")]
    public void TheEntryAndTheContestWriteAResourceTheSameWay(ulong hash, string? name)
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (hash, name)),
            Carrying("rdp_b.archive", (hash, name)));

        Assert.Equal(ResourceDisplay.Of(hash, name), new ArchiveEntry(hash, name, 1, 1).Display);
        Assert.Equal(ResourceDisplay.Of(hash, name), Assert.Single(contested.Contests).Display);
    }

    /// <summary>
    /// An archive carrying one resource twice carries it once for precedence.
    /// </summary>
    /// <remarks>
    /// A contest is between archives, not between the rows an index happens to
    /// hold. Deriving the leading set from rows lets one archive tie itself and
    /// report no winner for a contest the law decides outright.
    /// </remarks>
    [Fact]
    public void OneArchiveCarryingAResourceTwiceStillWinsItOutright()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath), (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        var contest = Assert.Single(contested.Contests);
        Assert.Equal("rdp_a.archive", contest.Winner);
        Assert.True(contest.HasDeterminedWinner);
        Assert.Empty(contest.UndeterminedAmong);
        Assert.Equal(
            new[] { "rdp_a.archive", "rdp_b.archive" },
            contest.Carriers.Select(carrier => carrier.FileName));
    }

    /// <summary>
    /// Two spellings of one archive name are one carrier.
    /// </summary>
    /// <remarks>
    /// Names are matched case-insensitively, so the two spellings answer to one
    /// load position. Counting them as two carriers would tie that position
    /// with itself and report no winner for a contest a third archive loses
    /// outright.
    /// </remarks>
    [Fact]
    public void TwoSpellingsOfOneArchiveNameAreOneCarrier()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("Foo.archive", (1, ContestedPath)),
            Carrying("foo.archive", (1, ContestedPath)),
            Carrying("rdp_z.archive", (1, ContestedPath)));

        var contest = Assert.Single(contested.Contests);
        Assert.Equal("Foo.archive", contest.Winner);
        Assert.True(contest.HasDeterminedWinner);
        Assert.Empty(contest.UndeterminedAmong);
        Assert.Equal(
            new[] { "Foo.archive", "rdp_z.archive" },
            contest.Carriers.Select(carrier => carrier.FileName));
    }

    /// <summary>
    /// One archive carrying a resource twice is not a contest with itself.
    /// </summary>
    [Fact]
    public void AResourceOneArchiveCarriesTwiceIsNoContest()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath), (1, ContestedPath)));

        Assert.Empty(contested.Contests);
    }

    /// <summary>
    /// An order computed over another reading is refused by kind.
    /// </summary>
    /// <remarks>
    /// The dangerous arm is the one where nothing is missing: every carrier
    /// still finds a position, so the contest is decided by ranks measured for
    /// a different set of archives and the winner reported is simply wrong.
    /// </remarks>
    [Fact]
    public void AnOrderFromAnotherReadingIsRefusedRatherThanDecidingContestsByIt()
    {
        var here = Inventory(
            Carrying("rdp_b.archive", (1, ContestedPath)),
            Carrying("rdp_c.archive", (1, ContestedPath)));
        var elsewhere = Inventory(
            Carrying("rdp_b.archive", (1, ContestedPath)),
            Carrying("rdp_c.archive", (1, ContestedPath)),
            Carrying("rdp_a.archive", (9, @"base\rdp\other.json")));

        var failure = Assert.Throws<ArchiveReadException>(
            () => ContestedSet.Of(here, ArchiveLoadOrder.Of(elsewhere, Modlist.Absent)));

        Assert.Equal(ArchiveFailureKind.MismatchedLoadOrder, failure.Kind);
        Assert.Contains("rdp_a.archive", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An order that does not cover these archives is refused, not crashed on.
    /// </summary>
    [Fact]
    public void AnOrderThatCoversNoneOfTheseArchivesIsRefusedByKind()
    {
        var here = Inventory(Carrying("rdp_b.archive", (1, ContestedPath)));
        var elsewhere = Inventory(Carrying("rdp_z.archive", (1, ContestedPath)));

        var failure = Assert.Throws<ArchiveReadException>(
            () => ContestedSet.Of(here, ArchiveLoadOrder.Of(elsewhere, Modlist.Absent)));

        Assert.Equal(ArchiveFailureKind.MismatchedLoadOrder, failure.Kind);
        Assert.Contains("rdp_b.archive", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two readings of one unchanged directory are still two readings.
    /// </summary>
    /// <remarks>
    /// The arm a caller reaches by reading the same directory twice and pairing
    /// the second reading with the first order, which is the ordinary way to
    /// get this wrong. Nothing differs between them, so the refusal carries the
    /// whole message here - including how many names they agree on, which is
    /// what tells a reader the two are the same directory and not two.
    /// </remarks>
    [Fact]
    public void TwoReadingsOfOneDirectoryAreRefusedRatherThanTakenForOne()
    {
        var first = Inventory(
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));
        var second = Inventory(
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        var failure = Assert.Throws<ArchiveReadException>(
            () => ContestedSet.Of(second, ArchiveLoadOrder.Of(first, Modlist.Absent)));

        Assert.Equal(ArchiveFailureKind.MismatchedLoadOrder, failure.Kind);
        Assert.Contains(
            "both cover the same 2 archive name(s), so they are two readings rather than one",
            failure.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The order built from this reading is the one that resolves it.
    /// </summary>
    /// <remarks>
    /// The accepting arm of the same conditional. Without it a refusal that
    /// fired on everything would satisfy the two arms above.
    /// </remarks>
    [Fact]
    public void TheOrderBuiltFromThisReadingResolvesIt()
    {
        var inventory = Inventory(
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        var contested = ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, Modlist.Absent));

        Assert.Equal("rdp_a.archive", Assert.Single(contested.Contests).Winner);
    }

    /// <summary>
    /// A resource two unlisted archives lose is one resource, not two.
    /// </summary>
    /// <remarks>
    /// The per-archive tallies answer "how did this archive fare"; summing
    /// them counts a resource once per archive that lost it, which overstates
    /// the hazard this wave exists to report.
    /// </remarks>
    [Fact]
    public void AResourceLostByTwoUnlistedArchivesIsCountedOnce()
    {
        var contested = Resolve(
            ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (1, ContestedPath)),
            Carrying("rdp_u1.archive", (1, ContestedPath)),
            Carrying("rdp_u2.archive", (1, ContestedPath)));

        Assert.Equal(1, contested.ResourcesLostToTheList);
        Assert.Equal(2, contested.Demotions.Sum(demotion => demotion.ContestsLostToListedArchives));
    }

    /// <summary>
    /// Every contested resource the list takes is counted.
    /// </summary>
    [Fact]
    public void EachResourceTheListTakesFromAnUnlistedArchiveIsCounted()
    {
        var contested = Resolve(
            ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (1, ContestedPath), (2, @"base\rdp\second.json")),
            Carrying("rdp_u1.archive", (1, ContestedPath), (2, @"base\rdp\second.json")));

        Assert.Equal(2, contested.ResourcesLostToTheList);
    }

    /// <summary>
    /// A contest between two unlisted archives is nobody's loss to the list.
    /// </summary>
    [Fact]
    public void AResourceNoListedArchiveCarriesIsNotCountedAsLostToTheList()
    {
        var contested = Resolve(
            ["rdp_listed.archive"],
            Carrying("rdp_listed.archive", (9, @"base\rdp\elsewhere.json")),
            Carrying("rdp_u1.archive", (1, ContestedPath)),
            Carrying("rdp_u2.archive", (1, ContestedPath)));

        Assert.Equal(0, contested.ResourcesLostToTheList);
        Assert.Equal(1, contested.UndeterminedCount);
    }

    /// <summary>
    /// With no list file nothing is lost to one, and that is not a measurement
    /// of what is being shadowed.
    /// </summary>
    [Fact]
    public void WithNoListFileNothingIsLostToTheList()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath)),
            Carrying("rdp_b.archive", (1, ContestedPath)));

        Assert.Equal(0, contested.ResourcesLostToTheList);
        Assert.Empty(contested.Demotions);
        Assert.Single(contested.Contests);
    }

    [Fact]
    public void TheBasisIsStatedAndTheResourcesItDidNotExamineAreCounted()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (1, ContestedPath), (2, @"base\rdp\a.json")),
            Carrying("rdp_b.archive", (1, ContestedPath), (3, @"base\rdp\b.json")));

        Assert.Equal(ContestBasis.ResourcePath, contested.Basis);
        Assert.Equal(3, contested.DistinctResourceCount);
        Assert.Equal(1, contested.ContestedCount);
        Assert.Equal(2, contested.ResourcesUncontestedAtThisBasis);
    }

    /// <summary>
    /// Contests come back ordered by hash, whatever order they were met in.
    /// </summary>
    /// <remarks>
    /// The hashes here are carried in descending order, so a set that handed
    /// back the order it accumulated them in would fail. Two resolutions in one
    /// process agree whether or not anything sorts, which is why agreement is
    /// not what is asserted.
    /// </remarks>
    [Fact]
    public void ContestsComeBackOrderedByHashRatherThanInTheOrderTheyWereMet()
    {
        var contested = Resolve(
            modlist: null,
            Carrying("rdp_a.archive", (9, @"base\rdp\c.json"), (5, @"base\rdp\b.json"), (1, ContestedPath)),
            Carrying("rdp_b.archive", (9, @"base\rdp\c.json"), (5, @"base\rdp\b.json"), (1, ContestedPath)));

        Assert.Equal(
            new ulong[] { 1, 5, 9 },
            contested.Contests.Select(contest => contest.Hash));
    }

    private static ArchiveContents Carrying(string fileName, params (ulong Hash, string? Name)[] entries) =>
        ArchiveContents.Read(
            fileName,
            entries.Select(entry => new ArchiveEntry(entry.Hash, entry.Name, 1, 1)).ToList());

    private static ArchiveInventory Inventory(params ArchiveContents[] archives) =>
        new(archives, [], default);

    private static ContestedSet Resolve(string[]? modlist, params ArchiveContents[] archives)
    {
        var inventory = new ArchiveInventory(archives, [], default);
        var list = modlist is null ? Modlist.Absent : Modlist.Of(modlist);

        return ContestedSet.Of(inventory, ArchiveLoadOrder.Of(inventory, list));
    }
}
