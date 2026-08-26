namespace Ripperdoc.Core.Archive;

/// <summary>
/// Every resource a mod directory's archives contest, resolved to the winner
/// the measured law names.
/// </summary>
/// <remarks>
/// Computed from the indices already in the inventory. No archive is reopened,
/// because a per-query open is the cost shape that makes a whole-install answer
/// impossible at real scale.
/// <para>
/// Three things this artifact says about itself, because each of them is a way
/// its numbers could otherwise be read as more than they are: what the contests
/// were computed over (<see cref="Basis" />), how many resources that basis did
/// not examine (<see cref="ResourcesUncontestedAtThisBasis" />), and whether
/// every archive was actually read (<see cref="IsComplete" />).
/// </para>
/// </remarks>
public sealed class ContestedSet
{
    private ContestedSet(
        IReadOnlyList<ContestedResource> contests,
        ArchiveLoadOrder order,
        int distinctResourceCount,
        IReadOnlyList<string> unreadArchives,
        IReadOnlyList<ArchiveDemotion> demotions)
    {
        Contests = contests;
        Order = order;
        DistinctResourceCount = distinctResourceCount;
        UnreadArchives = unreadArchives;
        Demotions = demotions;
    }

    /// <summary>
    /// Every contested resource, in a fixed order by hash so that two runs over
    /// an unchanged directory produce the same list.
    /// </summary>
    public IReadOnlyList<ContestedResource> Contests { get; }

    /// <summary>The load order these contests were resolved under.</summary>
    public ArchiveLoadOrder Order { get; }

    /// <summary>
    /// What these contests were computed over. It governs every result in this
    /// set - one computation produced them all.
    /// </summary>
    public ContestBasis Basis => ContestBasis.ResourcePath;

    /// <summary>
    /// How many distinct resources the archives that were read carry.
    /// </summary>
    public int DistinctResourceCount { get; }

    /// <summary>How many of those more than one archive carries.</summary>
    public int ContestedCount => Contests.Count;

    /// <summary>How many contests the law does not decide.</summary>
    public int UndeterminedCount => Contests.Count(contest => !contest.HasDeterminedWinner);

    /// <summary>
    /// How many resources this basis found no contest for.
    /// </summary>
    /// <remarks>
    /// The size of the population a contest invisible to <see cref="Basis" />
    /// would be hiding in, which is as close to quantifying that blind spot as
    /// a computation blind to it can get. It is not a count of resource-level
    /// contests, and nothing here says how many of those there are - only how
    /// many resources were never examined for one.
    /// </remarks>
    public int ResourcesUncontestedAtThisBasis => DistinctResourceCount - ContestedCount;

    /// <summary>
    /// Archives in the mod directory whose index could not be read, so nothing
    /// is known about what they carry.
    /// </summary>
    public IReadOnlyList<string> UnreadArchives { get; }

    /// <summary>
    /// Whether every archive the mod directory itself holds contributed its
    /// entries.
    /// </summary>
    /// <remarks>
    /// False means these contests are computed over part of that set. An
    /// archive nothing could read may carry any of these resources, and where
    /// it ranks first it would win one - so an incomplete set can name a winner
    /// that is not the winner, and says so here rather than presenting itself
    /// as the whole picture.
    /// <para>
    /// It says nothing about archives in subdirectories. Whether the game loads
    /// those is not measured, so they are not ordered, not resolved, and
    /// reported by the inventory instead - which is the one place that fact
    /// lives.
    /// </para>
    /// </remarks>
    public bool IsComplete => UnreadArchives.Count == 0;

    /// <summary>
    /// Every archive the list file does not name that is part of a contest.
    /// </summary>
    /// <remarks>
    /// Empty when the directory has no list file, because the hazard is
    /// specific to one: with no list, precedence follows file names, and a
    /// losing archive can be promoted by renaming it. Under a list it cannot.
    /// </remarks>
    public IReadOnlyList<ArchiveDemotion> Demotions { get; }

    /// <summary>
    /// Resolves an inventory's contests under a load order.
    /// </summary>
    /// <param name="inventory">The archives and what they carry.</param>
    /// <param name="order">
    /// The load order for the same directory. Its ranks are what decide every
    /// contest here.
    /// </param>
    public static ContestedSet Of(ArchiveInventory inventory, ArchiveLoadOrder order)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(order);

        if (!order.Orders(inventory))
        {
            throw ArchiveFailure.Failure(
                ArchiveFailureKind.MismatchedLoadOrder, order.DisagreementWith(inventory), inner: null);
        }

        var carriedBy = new Dictionary<ulong, List<Carrier>>();
        foreach (var archive in inventory.Archives)
        {
            foreach (var entry in archive.Entries)
            {
                if (!carriedBy.TryGetValue(entry.Hash, out var carriers))
                {
                    carriers = [];
                    carriedBy[entry.Hash] = carriers;
                }

                carriers.Add(new Carrier(archive.FileName, entry.Name));
            }
        }

        var contests = new List<ContestedResource>();
        foreach (var pair in carriedBy.OrderBy(pair => pair.Key))
        {
            // Whether this is a contest at all is decided by the same
            // computation that resolves one, so the two cannot disagree about
            // how many archives carry the resource.
            if (Resolve(pair.Key, pair.Value, order) is { } contest)
            {
                contests.Add(contest);
            }
        }

        var unread = inventory.Archives
            .Where(archive => !archive.WasRead)
            .Select(archive => archive.FileName)
            .ToList();

        return new ContestedSet(
            contests,
            order,
            inventory.DistinctEntryCount,
            unread,
            Demoted(contests, order));
    }

    private static ContestedResource? Resolve(ulong hash, List<Carrier> carriers, ArchiveLoadOrder order)
    {
        var ranked = carriers
            .Select(carrier => order.PositionOf(carrier.FileName)!.Value)
            // A contest is between archives, not between index rows. One
            // archive can hold a hash more than once, and two spellings of one
            // name answer to one position; either would otherwise let an
            // archive tie itself and unmake a winner the law names.
            .DistinctBy(position => position.FileName, ArchiveFileNames.Comparer)
            .Select(position => new ContestCarrier(position.FileName, position.Rank, position.IsListed))
            .OrderBy(carrier => carrier.Rank)
            .ThenBy(carrier => carrier.FileName, StringComparer.Ordinal)
            .ToList();

        if (ranked.Count < 2)
        {
            return null;
        }

        var leadingRank = ranked[0].Rank;
        var leading = ranked.Where(carrier => carrier.Rank == leadingRank).ToList();
        var shadowed = ranked
            .Where(carrier => carrier.Rank != leadingRank)
            .Select(carrier => carrier.FileName)
            .ToList();

        return new ContestedResource(
            hash,
            // The same resource can be named by one archive and nameless in
            // another, so a name from any carrier names the resource.
            carriers.Select(carrier => carrier.Name).FirstOrDefault(name => !string.IsNullOrEmpty(name)),
            ranked,
            leading.Count == 1 ? leading[0].FileName : null,
            leading.Count == 1 ? [] : leading.Select(carrier => carrier.FileName).ToList(),
            shadowed);
    }

    private static List<ArchiveDemotion> Demoted(
        IReadOnlyList<ContestedResource> contests,
        ArchiveLoadOrder order)
    {
        if (!order.Modlist.IsPresent)
        {
            return [];
        }

        var tallies = new Dictionary<string, Tally>(StringComparer.Ordinal);

        foreach (var contest in contests)
        {
            var wonByListed = contest.Carriers.Any(
                carrier => carrier.IsListed && carrier.FileName == contest.Winner);

            foreach (var carrier in contest.Carriers.Where(carrier => !carrier.IsListed))
            {
                tallies.TryGetValue(carrier.FileName, out var tally);

                tallies[carrier.FileName] = new Tally(
                    tally.Carried + 1,
                    tally.LostToListed + (wonByListed ? 1 : 0),
                    tally.Undetermined + (contest.HasDeterminedWinner ? 0 : 1));
            }
        }

        return tallies
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ArchiveDemotion(
                pair.Key, pair.Value.Carried, pair.Value.LostToListed, pair.Value.Undetermined))
            .ToList();
    }

    private readonly record struct Carrier(string FileName, string? Name);

    private readonly record struct Tally(int Carried, int LostToListed, int Undetermined);
}
