namespace Ripperdoc.Core.Archive;

/// <summary>
/// The mod directory's <c>modlist.txt</c>, as read.
/// </summary>
/// <remarks>
/// Whether the file is there at all decides which branch of the measured
/// precedence law applies, so its absence is a state this type carries rather
/// than an empty list standing in for one. An absent list orders every archive
/// by filename; a present one puts what it names first, and that difference is
/// the whole of the law.
/// <para>
/// Nothing is read into a line beyond a filename. No comment syntax, no
/// section marker and no path semantics were measured, so a line this project
/// cannot match against an archive is reported as naming nothing rather than
/// interpreted - see <see cref="ArchiveLoadOrder.ListedButNotPresent" />.
/// Guessing a comment character would silently drop a line the game may well
/// be honouring.
/// </para>
/// </remarks>
public sealed class Modlist
{
    /// <summary>The file the game reads the archive order from.</summary>
    public const string FileName = "modlist.txt";

    private Modlist(bool isPresent, IReadOnlyList<string> listedNames, IReadOnlyList<string> repeatedNames)
    {
        IsPresent = isPresent;
        ListedNames = listedNames;
        RepeatedNames = repeatedNames;
    }

    /// <summary>Whether the mod directory has a list file at all.</summary>
    public bool IsPresent { get; }

    /// <summary>
    /// The archive names the file gives, in the order it gives them, with a
    /// name that appears more than once kept at its first appearance.
    /// </summary>
    public IReadOnlyList<string> ListedNames { get; }

    /// <summary>
    /// Names the file gives more than once, each reported once.
    /// </summary>
    /// <remarks>
    /// What the game does with a repeated name is not measured. A repeat is
    /// held at its first appearance because that is the position the archive
    /// would occupy under the measured rule if it loads once, and it is
    /// reported here because a caller acting on the order deserves to know the
    /// input said the same thing twice.
    /// </remarks>
    public IReadOnlyList<string> RepeatedNames { get; }

    /// <summary>How many archives the file names, repeats counted once.</summary>
    public int ListedCount => ListedNames.Count;

    /// <summary>A mod directory with no list file.</summary>
    public static Modlist Absent { get; } = new(isPresent: false, [], []);

    /// <summary>
    /// Reads <paramref name="modDirectory" />'s list file, or reports that it
    /// has none.
    /// </summary>
    /// <exception cref="ArchiveReadException">
    /// The file is there and could not be read.
    /// </exception>
    public static Modlist Read(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);

        var path = Path.Combine(modDirectory, FileName);
        if (!File.Exists(path))
        {
            return Absent;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception exception)
        {
            throw ArchiveFailure.Failure(
                ArchiveFailure.Classify(
                    exception, ArchiveFailureKind.UnreadableModlist, ArchiveOperation.FileRead),
                path,
                exception);
        }

        return Of(lines);
    }

    /// <summary>
    /// The same reading, over lines already in hand.
    /// </summary>
    /// <remarks>
    /// Separate from the file read so that the ordering law can be exercised
    /// over a list without one being written to disk, and so that the two
    /// things that can go wrong - the read and the reading - stay apart.
    /// </remarks>
    public static Modlist Of(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var listed = new List<string>();
        var repeated = new List<string>();
        var seen = new HashSet<string>(ArchiveFileNames.Comparer);

        foreach (var line in lines)
        {
            var name = line.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (seen.Add(name))
            {
                listed.Add(name);
            }
            else if (!repeated.Contains(name, ArchiveFileNames.Comparer))
            {
                repeated.Add(name);
            }
        }

        return new Modlist(isPresent: true, listed, repeated);
    }
}
