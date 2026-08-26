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
    private readonly ArchiveInventory _inventory;

    private ArchiveLoadOrder(
        IReadOnlyList<ArchiveLoadPosition> positions,
        Modlist modlist,
        IReadOnlyList<string> listedButNotPresent,
        ArchiveInventory inventory)
    {
        Positions = positions;
        Modlist = modlist;
        ListedButNotPresent = listedButNotPresent;
        _inventory = inventory;

        _byName = new Dictionary<string, ArchiveLoadPosition>(ArchiveFileNames.Comparer);
        foreach (var position in positions)
        {
            // Two files whose names differ only in case cannot exist in one
            // Windows directory, and a tree copied to a file system where they
            // can is not a reason to end a read.
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
    /// Whether this order was computed over <paramref name="inventory" />.
    /// </summary>
    /// <remarks>
    /// Identity, not equal contents. Two readings of one directory can hold the
    /// same archive names and still rank them differently - a list file
    /// rewritten between the two is enough - so a comparison of what they
    /// contain would accept the pairing that decides contests by the wrong
    /// ranks.
    /// </remarks>
    internal bool Orders(ArchiveInventory inventory) => ReferenceEquals(_inventory, inventory);

    /// <summary>
    /// How <paramref name="inventory" /> differs from what this order covers.
    /// </summary>
    internal string DisagreementWith(ArchiveInventory inventory)
    {
        var mine = _inventory.Archives.Select(archive => archive.FileName).ToList();
        var theirs = inventory.Archives.Select(archive => archive.FileName).ToList();

        var onlyTheirs = theirs.Except(mine, ArchiveFileNames.Comparer).Order(StringComparer.Ordinal).ToList();
        var onlyMine = mine.Except(theirs, ArchiveFileNames.Comparer).Order(StringComparer.Ordinal).ToList();

        if (onlyTheirs.Count == 0 && onlyMine.Count == 0)
        {
            return $"both cover the same {mine.Count} archive name(s), so they are two readings rather "
                + "than one";
        }

        var parts = new List<string>();
        if (onlyTheirs.Count > 0)
        {
            parts.Add($"it does not cover {string.Join(", ", onlyTheirs)}");
        }

        if (onlyMine.Count > 0)
        {
            parts.Add($"it ranks {string.Join(", ", onlyMine)}, which is not among them");
        }

        return string.Join("; ", parts);
    }

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

            return new ArchiveLoadOrder(byName, modlist, [], inventory);
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
            // Ordering by rank only. The names went in sorted and this sort is
            // stable, so archives sharing a rank keep that order; a second key
            // here would be a third guarantee of the same thing, and it was
            // masking which one actually holds it.
            .OrderBy(position => position.Rank)
            .ToList();

        var present = new HashSet<string>(fileNames, ArchiveFileNames.Comparer);
        var absent = modlist.ListedNames.Where(name => !present.Contains(name)).ToList();

        return new ArchiveLoadOrder(positions, modlist, absent, inventory);
    }
}
