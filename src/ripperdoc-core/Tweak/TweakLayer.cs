namespace Ripperdoc.Core.Tweak;

/// <summary>
/// The tweak directory, enumerated in the order the framework reads it.
/// </summary>
/// <remarks>
/// <para>
/// Order is the whole product here. Two files writing the same value produce no
/// warning from the framework applying them, so the only way to say which mod
/// set a value is to reproduce the read order exactly and watch the value change
/// hands. A walk that is nearly right names the wrong mod, and nothing
/// contradicts it.
/// </para>
/// <para>
/// Every file found under the directory is carried, including the ones the
/// framework does not read. A file that is present and unread is a fact about
/// the layer - the reader needs to know a format was seen and passed over,
/// rather than have it vanish between the directory and the report.
/// </para>
/// </remarks>
public sealed class TweakLayer
{
    /// <summary>
    /// The characters that, as the first character of a file's own name, put it
    /// in the first group.
    /// </summary>
    /// <remarks>
    /// The test is on the file's name, not on any directory above it, so a file
    /// deep in an ordinary directory is promoted while its siblings are not.
    /// </remarks>
    public const string FirstGroupMarkers = "_#$!";

    /// <summary>
    /// The characters that, as the first character of a file's own name, put it
    /// in the last group.
    /// </summary>
    public const string LastGroupMarkers = "^";

    private TweakLayer(
        IReadOnlyList<TweakFile> files,
        IReadOnlyList<TweakFile> present,
        bool enumerationIsCollated)
    {
        Files = files;
        Present = present;
        EnumerationIsCollated = enumerationIsCollated;
    }

    /// <summary>
    /// The files the framework reads, in the order it reads them.
    /// </summary>
    /// <remarks>
    /// Apply order is read order, so the position of a file in this list is the
    /// position from which its writes are judged.
    /// </remarks>
    public IReadOnlyList<TweakFile> Files { get; }

    /// <summary>
    /// Every file under the directory, in walk order, read and unread alike.
    /// </summary>
    public IReadOnlyList<TweakFile> Present { get; }

    /// <summary>
    /// Whether the directory enumeration this walk was built from is already in
    /// a case-insensitive collation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework does not sort. It reads the order the filesystem hands it,
    /// which on a case-insensitively collated volume is the collation and on
    /// others is not - creation order, for instance. Both possibilities produce
    /// the same answer on a collated volume and different winners on one that is
    /// not, so the tool checks rather than assumes: this is false when the
    /// enumeration and the collation disagree, and every ordering claim over
    /// such a layer is qualified rather than asserted.
    /// </para>
    /// <para>
    /// The comparison is ordinal and case-insensitive. That is the collation the
    /// ordering measurement was taken under; a volume whose own collation
    /// differs from it in some other respect would show up here as a
    /// disagreement, which is the outcome that keeps the claim honest.
    /// </para>
    /// <para>
    /// What this does is report. It does not change the walk and it does not
    /// qualify anything by itself - the order used is the enumeration's either
    /// way, because that is the order the framework consumes.
    /// </para>
    /// <para>
    /// Carrying it is the job of whoever holds the layer. It is on the layer
    /// and reachable from a resolved state through
    /// <see cref="TweakResolvedState.Layer"/>, and it is not carried any
    /// further down: a contest on its own does not hold it and cannot fetch
    /// it. So a report qualifies its ordering conclusions where the layer is in
    /// hand, which is the only place that can: a duty assigned to a caller with
    /// no route to the flag is one nobody can discharge.
    /// </para>
    /// </remarks>
    public bool EnumerationIsCollated { get; }

    /// <summary>
    /// The files present but not read by the framework, in walk order.
    /// </summary>
    public IEnumerable<TweakFile> Unread => Present.Where(file => file.Format == TweakFileFormat.NotRead);

    /// <summary>
    /// Walk a tweak directory.
    /// </summary>
    /// <param name="directoryPath">The tweak directory.</param>
    /// <returns>The layer, in read order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="directoryPath"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">There is no directory at that path.</exception>
    public static TweakLayer Enumerate(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"There is no tweak directory at '{directoryPath}'.");
        }

        var walked = new List<string>();
        var collated = true;
        Walk(directoryPath, string.Empty, walked, ref collated);

        return Of(walked, collated);
    }

    /// <summary>
    /// Build a layer from a walk that has already been performed.
    /// </summary>
    /// <param name="walked">
    /// Every file under the layer, in the order the walk reached them, each by
    /// its path within the layer.
    /// </param>
    /// <param name="enumerationIsCollated">
    /// Whether the enumeration that walk came from was already in a
    /// case-insensitive collation.
    /// </param>
    /// <returns>The layer, in read order.</returns>
    /// <remarks>
    /// <para>
    /// The grouping and the read positions are a function of the walk and of
    /// nothing else, so they are computed here rather than inside the directory
    /// walk. What follows is that they can be exercised against a stated order:
    /// a real directory hands back whatever order the filesystem holds, which
    /// differs between filesystems, so a check that both writes the files and
    /// asserts the order they come back in is asserting a property of the
    /// volume it happens to run on.
    /// </para>
    /// <para>
    /// It is also the seam a reader that does not walk a directory needs - a
    /// layer presented through a manager's virtual filesystem arrives as an
    /// ordered list, not as a path to enumerate.
    /// </para>
    /// <para>
    /// Whether the enumeration was collated is stated by the caller rather than
    /// worked out from <paramref name="walked"/>. The answer is a property of
    /// each directory's own enumeration and this list is the flattened result of
    /// several, so deriving it here would be a second rule for one fact, free to
    /// disagree with the one the walk applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="walked"/> is null.</exception>
    public static TweakLayer Of(IReadOnlyList<string> walked, bool enumerationIsCollated)
    {
        ArgumentNullException.ThrowIfNull(walked);

        // Grouped before positions are handed out, because the position a file
        // is reported at has to be the position its writes are judged from. A
        // position taken from the walk would disagree with the order the files
        // are actually applied in, which is the one thing this type exists to
        // get right.
        var present = new List<TweakFile>(walked.Count);
        var read = new List<TweakFile>(walked.Count);

        foreach (var group in new[] { TweakFileGroup.First, TweakFileGroup.Normal, TweakFileGroup.Last })
        {
            foreach (var relativePath in walked)
            {
                if (GroupOf(relativePath) != group)
                {
                    continue;
                }

                var format = FormatOf(relativePath);
                if (format == TweakFileFormat.NotRead)
                {
                    continue;
                }

                read.Add(new TweakFile(relativePath, group, format, read.Count + 1));
            }
        }

        var readByPath = read.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        foreach (var relativePath in walked)
        {
            present.Add(readByPath.TryGetValue(relativePath, out var file)
                ? file
                : new TweakFile(relativePath, GroupOf(relativePath), TweakFileFormat.NotRead, 0));
        }

        return new TweakLayer(read, present, enumerationIsCollated);
    }

    /// <summary>
    /// Whether a directory enumeration is already in a case-insensitive
    /// collation.
    /// </summary>
    /// <param name="entries">The entries, in the order the filesystem gave them.</param>
    /// <returns>False if a collation would have put them in a different order.</returns>
    /// <remarks>
    /// Separated from the walk so that both answers can be checked. On a volume
    /// whose enumeration is already collated - which is every volume this is
    /// developed and tested on - the false answer is unreachable through a
    /// directory, and a branch that cannot be reached is a branch nobody knows
    /// the behaviour of.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
    public static bool IsCollated(IReadOnlyList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        for (var index = 1; index < entries.Count; index++)
        {
            if (StringComparer.OrdinalIgnoreCase.Compare(entries[index - 1], entries[index]) > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Which group a file's own name puts it in.
    /// </summary>
    /// <param name="relativePath">The file's path within the layer.</param>
    /// <returns>The group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is null.</exception>
    public static TweakFileGroup GroupOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var name = NameOf(relativePath);
        if (name.Length == 0)
        {
            return TweakFileGroup.Normal;
        }

        if (FirstGroupMarkers.Contains(name[0], StringComparison.Ordinal))
        {
            return TweakFileGroup.First;
        }

        return LastGroupMarkers.Contains(name[0], StringComparison.Ordinal)
            ? TweakFileGroup.Last
            : TweakFileGroup.Normal;
    }

    /// <summary>
    /// Which reader, if any, the framework gives a file of this name to.
    /// </summary>
    /// <param name="relativePath">The file's path within the layer.</param>
    /// <returns>The format, or <see cref="TweakFileFormat.NotRead"/>.</returns>
    /// <remarks>
    /// The extension match is case-sensitive, so a file whose extension is
    /// spelled in capitals is not read at all. That is reported as a file the
    /// framework passes over rather than corrected into a file it reads,
    /// because reading one the game will not read would put values in the
    /// resolved state that are not in the game's.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is null.</exception>
    public static TweakFileFormat FormatOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return Path.GetExtension(NameOf(relativePath)) switch
        {
            ".yaml" or ".yml" => TweakFileFormat.Yaml,
            ".tweak" => TweakFileFormat.Red,
            _ => TweakFileFormat.NotRead,
        };
    }

    // The file's own name, taken with the separator a layer path is written
    // with rather than with whatever the running platform divides a path on.
    // The two disagree in both directions - a platform where '\' is an
    // ordinary character reads a whole layer path as one name, and one where
    // '/' divides reads a name carrying it as two - and either way the
    // character a file's group is decided by would come from somewhere other
    // than the file's name. The answer has to be the same on every platform,
    // because it decides which mod a report names.
    private static string NameOf(string relativePath)
    {
        var separator = relativePath.LastIndexOf(TweakFile.PathSeparator);

        return separator < 0 ? relativePath : relativePath[(separator + 1)..];
    }

    // Pre-order, with a directory's contents taken at the directory's own
    // position rather than after its siblings, and with files and directories
    // taken from one enumeration rather than two passes. The enumeration order
    // is used as it arrives, because that is what the framework consumes; what
    // a collation would have produced is computed alongside it only to say
    // whether the two agree.
    private static void Walk(string directoryPath, string prefix, List<string> walked, ref bool collated)
    {
        var entries = Directory.EnumerateFileSystemEntries(directoryPath).ToArray();

        if (!IsCollated(entries))
        {
            collated = false;
        }

        foreach (var entry in entries)
        {
            var relativePath = prefix.Length == 0
                ? Path.GetFileName(entry)
                : prefix + TweakFile.PathSeparator + Path.GetFileName(entry);

            if (Directory.Exists(entry))
            {
                Walk(entry, relativePath, walked, ref collated);
            }
            else
            {
                walked.Add(relativePath);
            }
        }
    }
}

/// <summary>
/// One file in a tweak layer.
/// </summary>
/// <param name="RelativePath">
/// The path within the layer, never the machine path it was found at.
/// </param>
/// <param name="Group">Which group the file's name puts it in.</param>
/// <param name="Format">Which reader the framework gives it to.</param>
/// <param name="ReadPosition">
/// Its one-based position in the read order, or zero where the framework does
/// not read it.
/// </param>
public readonly record struct TweakFile(
    string RelativePath,
    TweakFileGroup Group,
    TweakFileFormat Format,
    int ReadPosition)
{
    /// <summary>
    /// The separator this project reports layer paths with.
    /// </summary>
    /// <remarks>
    /// Fixed rather than taken from the running platform so that a path in a
    /// report reads the same wherever the report is read, and matches the form
    /// the framework's own log prints.
    /// </remarks>
    public const char PathSeparator = '\\';
}

/// <summary>
/// Which of the three groups a file is read in.
/// </summary>
/// <remarks>
/// The groups are read in this order, and the walk order applies within each
/// one. Declared in reading order so that ordering them is the enum's own
/// arithmetic rather than a table somewhere else.
/// </remarks>
public enum TweakFileGroup
{
    /// <summary>Read before every other file.</summary>
    First,

    /// <summary>Read after the first group and before the last.</summary>
    Normal,

    /// <summary>Read after every other file.</summary>
    Last,
}

/// <summary>
/// Which reader the framework gives a file to.
/// </summary>
public enum TweakFileFormat
{
    /// <summary>The framework does not read this file.</summary>
    NotRead,

    /// <summary>The YAML tweak language.</summary>
    Yaml,

    /// <summary>The other tweak language, which this engine does not replay.</summary>
    Red,
}
