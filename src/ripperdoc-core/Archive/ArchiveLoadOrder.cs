namespace Ripperdoc.Core.Archive;

/// <summary>
/// The order a mod directory's archives load in, under the measured
/// precedence law.
/// </summary>
/// <remarks>
/// The law, measured on game 2.31 and published as a finding:
/// <list type="number">
/// <item>With no list file, every archive loads in ASCII order by file name.</item>
/// <item>With a list file, the archives it names load first in the order it
/// names them, and then every archive it does not name.</item>
/// <item>The first-loaded archive wins, always.</item>
/// </list>
/// <para>
/// <strong>What the law does not say is not filled in here.</strong> The order
/// among the archives a present list does not name was not measured - in the
/// boots that established the law those archives contested nothing, so their
/// relative order was unobservable. ASCII order is the natural guess and it is
/// a guess, so this type gives them all one rank and lets the tie travel.
/// Guessing would produce a confident winner for a contest nothing has decided.
/// </para>
/// <para>
/// Ordering by file name is done here rather than taken from the inventory's
/// enumeration. The enumeration is ordered for reproducibility and says so; a
/// claim about load order belongs at the site that makes it.
/// </para>
/// </remarks>
public sealed class ArchiveLoadOrder
{
    private readonly Dictionary<string, ArchiveLoadPosition> _byName;

    private ArchiveLoadOrder(
        IReadOnlyList<ArchiveLoadPosition> positions,
        Modlist modlist,
        IReadOnlyList<string> listedButNotPresent)
    {
        Positions = positions;
        Modlist = modlist;
        ListedButNotPresent = listedButNotPresent;

        _byName = new Dictionary<string, ArchiveLoadPosition>(ArchiveFileNames.Comparer);
        foreach (var position in positions)
        {
            // First spelling wins rather than throwing. Two files whose names
            // differ only in case cannot exist in one Windows directory, and a
            // tree copied to a file system where they can is not a reason to
            // end a read - both answer to the one thing the list said about
            // that name.
            _byName.TryAdd(position.FileName, position);
        }
    }

    /// <summary>
    /// Every archive with its rank, lowest first.
    /// </summary>
    /// <remarks>
    /// Archives sharing a rank appear in file-name order so that two runs
    /// produce the same list. That ordering is presentational: within one rank
    /// this project does not know which loads first, and the sequence here must
    /// not be read as saying it does.
    /// </remarks>
    public IReadOnlyList<ArchiveLoadPosition> Positions { get; }

    /// <summary>The list file this order was computed against.</summary>
    public Modlist Modlist { get; }

    /// <summary>
    /// Names the list file gives that no archive in the directory answers to.
    /// </summary>
    /// <remarks>
    /// Reported rather than dropped: a line naming nothing is either a stale
    /// entry or a mod that failed to deploy, and both are things a reader of
    /// this order wants to know before trusting it.
    /// </remarks>
    public IReadOnlyList<string> ListedButNotPresent { get; }

    /// <summary>
    /// Whether every archive's place is known relative to every other's.
    /// </summary>
    /// <remarks>
    /// False exactly when two or more archives share a rank, which is the
    /// unmeasured residue rather than a defect. A contest whose leading
    /// carriers share a rank has no winner this project can name.
    /// </remarks>
    public bool IsFullyOrdered => Positions.Select(position => position.Rank).Distinct().Count() == Positions.Count;

    /// <summary>
    /// Where <paramref name="fileName" /> sits, or <see langword="null" /> when
    /// this order has no such archive.
    /// </summary>
    public ArchiveLoadPosition? PositionOf(string fileName) =>
        fileName is not null && _byName.TryGetValue(fileName, out var position) ? position : null;

    /// <summary>
    /// Computes the load order for an inventory under a list file.
    /// </summary>
    /// <param name="inventory">The archives the mod directory holds.</param>
    /// <param name="modlist">
    /// The directory's list file, or <see cref="Modlist.Absent" /> when it has
    /// none. Which of the two it is selects the branch of the law, so it is
    /// asked for rather than guessed from an empty list.
    /// </param>
    public static ArchiveLoadOrder Of(ArchiveInventory inventory, Modlist modlist)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(modlist);

        var fileNames = inventory.Archives
            .Select(archive => archive.FileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (!modlist.IsPresent)
        {
            var byName = fileNames
                .Select((name, index) => new ArchiveLoadPosition(name, index, IsListed: false))
                .ToList();

            return new ArchiveLoadOrder(byName, modlist, []);
        }

        var listedRanks = new Dictionary<string, int>(ArchiveFileNames.Comparer);
        for (var index = 0; index < modlist.ListedNames.Count; index++)
        {
            listedRanks[modlist.ListedNames[index]] = index;
        }

        // One rank for all of them, after every listed archive. This is the
        // whole of what was measured about an archive the list does not name.
        var unlistedRank = modlist.ListedCount;

        var positions = fileNames
            .Select(name => listedRanks.TryGetValue(name, out var rank)
                ? new ArchiveLoadPosition(name, rank, IsListed: true)
                : new ArchiveLoadPosition(name, unlistedRank, IsListed: false))
            .OrderBy(position => position.Rank)
            .ThenBy(position => position.FileName, StringComparer.Ordinal)
            .ToList();

        var present = new HashSet<string>(fileNames, ArchiveFileNames.Comparer);
        var absent = modlist.ListedNames.Where(name => !present.Contains(name)).ToList();

        return new ArchiveLoadOrder(positions, modlist, absent);
    }
}
