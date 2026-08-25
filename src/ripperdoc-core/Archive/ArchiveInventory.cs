namespace Ripperdoc.Core.Archive;

/// <summary>
/// Every archive a mod directory holds, and every resource each one carries.
/// </summary>
/// <remarks>
/// This is the archive layer's enumeration, and nothing more. It says what is
/// present; it does not say what wins. Precedence is a separate, measured law
/// and is not inferred from anything here - in particular
/// <see cref="Archives" /> is in a stated enumeration order that is
/// <em>not</em> load order, and reading it as load order would be wrong.
/// </remarks>
public sealed class ArchiveInventory
{
    internal ArchiveInventory(
        IReadOnlyList<ArchiveContents> archives,
        IReadOnlyList<string> nestedArchivePaths,
        InventoryProvenance provenance,
        ArchiveFailureKind? nestedListingFailureKind = null,
        string? nestedListingFailure = null)
    {
        Archives = archives;
        NestedArchivePaths = nestedArchivePaths;
        Provenance = provenance;
        NestedListingFailureKind = nestedListingFailureKind;
        NestedListingFailure = nestedListingFailure;
    }

    /// <summary>
    /// The archives found directly in the mod directory, ordered by file name
    /// under an ordinal comparison.
    /// </summary>
    /// <remarks>
    /// The order is fixed so that two runs over an unchanged directory produce
    /// the same list. It is an enumeration order chosen for reproducibility,
    /// and it carries no claim about which archive the game loads first.
    /// </remarks>
    public IReadOnlyList<ArchiveContents> Archives { get; }

    /// <summary>
    /// Archives found in subdirectories of the mod directory, relative to it.
    /// </summary>
    /// <remarks>
    /// Reported, and deliberately not included in <see cref="Archives" />.
    /// Whether the game loads an archive from a subdirectory is <em>not
    /// measured</em> by this project. Counting them among the loaded set would
    /// assert something unmeasured; leaving them out silently would hide files
    /// that are really there. So they are named here, separately, and a caller
    /// that reports them says the precedence is unmeasured.
    /// </remarks>
    public IReadOnlyList<string> NestedArchivePaths { get; }

    /// <summary>
    /// Why the subdirectories could not be listed, or <see langword="null" />
    /// when they were.
    /// </summary>
    /// <remarks>
    /// Recorded rather than thrown, the way <see cref="ArchiveContents" />
    /// records an archive it could not read. <see cref="Archives" /> is what the
    /// mod directory loads and this list is not, so a directory that cannot be
    /// listed says so beside a complete <see cref="Archives" /> instead of
    /// taking it down.
    /// </remarks>
    public string? NestedListingFailure { get; }

    /// <summary>
    /// Which kind of failure stopped the subdirectory listing, or
    /// <see langword="null" /> when it completed.
    /// </summary>
    public ArchiveFailureKind? NestedListingFailureKind { get; }

    /// <summary>What produced this inventory, and under which naming posture.</summary>
    public InventoryProvenance Provenance { get; }

    /// <summary>How many archives were found directly in the mod directory.</summary>
    public int ArchiveCount => Archives.Count;

    /// <summary>How many of those could not be read.</summary>
    public int UnreadableCount => Archives.Count(archive => !archive.WasRead);

    /// <summary>
    /// Every entry across every archive that was read, including the same
    /// resource carried by more than one archive.
    /// </summary>
    public IEnumerable<ArchiveEntry> AllEntries =>
        Archives.SelectMany(archive => archive.Entries);

    /// <summary>
    /// How many distinct resources the directory carries, counting a resource
    /// carried by several archives once.
    /// </summary>
    public int DistinctEntryCount => AllEntries.Select(entry => entry.Hash).Distinct().Count();

    /// <summary>
    /// How many distinct resources a naming source could name.
    /// </summary>
    /// <remarks>
    /// Counted over distinct resources rather than over entries, and a resource
    /// counts as named if <em>any</em> archive carrying it named it - the same
    /// resource can be named by one archive and nameless in another.
    /// </remarks>
    public int DistinctNamedCount =>
        AllEntries.GroupBy(entry => entry.Hash).Count(group => group.Any(entry => entry.IsNamed));

    /// <summary>
    /// How many distinct resources are reported by hash because no name was
    /// available for them.
    /// </summary>
    public int DistinctHashOnlyCount => DistinctEntryCount - DistinctNamedCount;
}
