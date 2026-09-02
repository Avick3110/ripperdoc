using System.Text;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// Which files of the state directory currently hold it, as the directory's own
/// version manifest says.
/// </summary>
/// <param name="Manifest">The manifest file the directory points at.</param>
/// <param name="Tables">The table files still live, in the order they are read.</param>
/// <param name="Logs">The write-ahead logs the manifest names.</param>
/// <param name="LogsNotListed">
/// Why the directory's logs could not be listed, or null where they were.
/// </param>
/// <remarks>
/// <para>
/// Which files hold state is read rather than guessed at from the directory
/// listing. A table the manifest has dropped stays on disk until the manager
/// deletes it, so a reader taking every table it can see resurrects whatever
/// the dropped one held.
/// </para>
/// <para>
/// The logs are the exception, and the directory is listed to refuse rather
/// than to read: a writer opens its next log before the edit naming it is
/// written, so a log numbered above the named one means the newest writes may
/// be somewhere this reader would pass over.
/// </para>
/// </remarks>
internal sealed record StateVersion(
    string Manifest,
    IReadOnlyList<string> Tables,
    IReadOnlyList<string> Logs,
    string? LogsNotListed)
{
    /// <summary>The file naming the manifest in force.</summary>
    internal const string PointerName = "CURRENT";

    /// <summary>The one key ordering this reader models.</summary>
    internal const string Comparator = "leveldb.BytewiseComparator";

    /// <summary>The extension this reader models a table file under.</summary>
    internal const string TableExtension = ".ldb";

    /// <summary>The extension this reader models a write-ahead log under.</summary>
    internal const string LogExtension = ".log";

    private const int ComparatorTag = 1;
    private const int LogNumberTag = 2;
    private const int NextFileNumberTag = 3;
    private const int LastSequenceTag = 4;
    private const int CompactPointerTag = 5;
    private const int DeletedFileTag = 6;
    private const int NewFileTag = 7;
    private const int PreviousLogNumberTag = 9;

    /// <summary>
    /// Reads which files are live, or reports that the directory holds no
    /// database.
    /// </summary>
    /// <param name="directory">The state directory.</param>
    /// <returns>The live set, or null where there is no pointer to read.</returns>
    /// <exception cref="StateReadException">
    /// The pointer, the manifest, or something either of them names is not what
    /// this reader models.
    /// </exception>
    internal static StateVersion? In(string directory)
    {
        byte[] pointer;

        try
        {
            pointer = StateFile.ReadAllBytes(Path.Combine(directory, PointerName));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Absence is decided here and nowhere else: no pointer, no
            // database. Anything that stops the pointer being read is a
            // database that is there and cannot be read, and letting that
            // return null would report every wanted mod as one the manager
            // never asked for.
            throw new StateReadException(
                $"'{PointerName}' in '{directory}' is there and could not be read: "
                + $"{error.Message.TrimEnd('.')}. That is a state directory this reader is "
                + "refused, not one that holds no database. Check that the path names the "
                + "manager's own state directory and that this process may read it.",
                error);
        }

        return Read(
            directory,
            PlainFileName.Named(
                Encoding.UTF8.GetString(pointer).Trim(),
                $"'{PointerName}' in '{directory}'",
                "the manifest in force"));
    }

    /// <summary>
    /// Reads a file the manifest names, turning its absence into a refusal that
    /// says whose word it was there on.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>Its bytes.</returns>
    /// <exception cref="StateReadException">The manifest names a file that is not there.</exception>
    internal byte[] Read(string path)
    {
        try
        {
            return StateFile.ReadAllBytes(path);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new StateReadException(
                $"'{Manifest}' says '{Path.GetFileName(path)}' holds part of the state and there "
                + "is no such file to read. The directory is missing a file its own manifest "
                + "names, so what it holds cannot be read whole - and reading the rest would "
                + "report whatever that file held as absent.",
                error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new StateReadException(
                $"'{Manifest}' says '{Path.GetFileName(path)}' holds part of the state and it is "
                + $"there and could not be read: {error.Message.TrimEnd('.')}. Reading the rest "
                + "would report whatever that file held as absent, on the strength of a "
                + "permission rather than of what the manager wrote.",
                error);
        }
    }

    private static StateVersion Read(string directory, PlainFileName manifest)
    {
        var named = manifest.Name;
        byte[] bytes;

        try
        {
            bytes = StateFile.ReadAllBytes(PlainFileName.Under(directory, manifest));
        }
        catch (Exception error)
            when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new StateReadException(
                $"'{PointerName}' names '{named}' and there is no such file in '{directory}'. "
                + "The pointer and the manifest it names disagree, so nothing here can say which "
                + "files hold the state - which is a damaged directory, not an empty one.",
                error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new StateReadException(
                $"'{PointerName}' names '{named}' in '{directory}' and it is there and could not "
                + $"be read: {error.Message.TrimEnd('.')}. The manifest is what says which files "
                + "hold the state, so a directory whose manifest this reader is refused holds a "
                + "state it cannot see rather than no state at all.",
                error);
        }

        var live = new Dictionary<ulong, int>();
        var logs = new LogNumbers();
        string? comparator = null;

        foreach (var record in LogRecords.In(bytes, named))
        {
            ReadEdit(record, named, live, logs, ref comparator);
        }

        if (comparator != Comparator)
        {
            throw new StateReadException(
                $"'{named}' declares its key ordering as '{comparator ?? "nothing at all"}', and "
                + $"this reader models only '{Comparator}'. Every merge below reads keys in that "
                + "order, so reading this database would be reading it in an order its writer did "
                + "not use.");
        }

        return new StateVersion(
            named,
            [.. live.Keys.Order().Select(number => Path.Combine(directory, Name(number, TableExtension)))],
            [.. new[] { logs.Current, logs.Previous }
                .Where(number => number is not (null or 0)).Select(number => number!.Value)
                .Distinct().Order()
                .Select(number => Path.Combine(directory, Name(number, LogExtension)))],
            Unnamed(directory, logs));
    }

    private static string Name(ulong number, string extension) => $"{number:D6}{extension}";

    /// <remarks>
    /// The one place this reader looks at the directory rather than at what the
    /// manifest names, and it looks in order to refuse. The format's own
    /// recovery reads every log at or above the number the manifest records,
    /// because a writer opens its next log before the edit naming it is
    /// written; reading only the named logs can therefore miss the newest
    /// writes with nothing to show for it.
    /// </remarks>
    private static string? Unnamed(string directory, LogNumbers logs)
    {
        var named = Math.Max(logs.Current ?? 0, logs.Previous ?? 0);
        List<string> present;

        try
        {
            present = [.. System.IO.Directory.EnumerateFiles(directory, "*" + LogExtension)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A directory this process may read files in but not list is one
            // where the question cannot be asked. Saying so beats a refusal
            // that blames the state, and beats a silence that would read as a
            // check that passed.
            return "whether a write-ahead log the manifest does not name is present could not be "
                + $"established: this directory could not be listed ({error.Message.TrimEnd('.')}). "
                + "The logs the manifest names were read; a newer one left by an interrupted "
                + "flush would not have been seen.";
        }

        foreach (var path in present.Order(StringComparer.Ordinal))
        {
            var file = Path.GetFileName(path);

            if (!ulong.TryParse(Path.GetFileNameWithoutExtension(path), out var number)
                || number <= named)
            {
                continue;
            }

            throw new StateReadException(
                $"'{file}' is a write-ahead log the manifest does not name, and it is numbered "
                + $"above the newest log the manifest does name ('{Name(named, LogExtension)}'). "
                + "The state may have been left mid-flush, in which case its newest writes are in "
                + "a file this reader would have passed over - so this is a state it cannot read "
                + "whole rather than one it can read in part.");
        }

        return null;
    }

    /// <remarks>
    /// The edits are applied in order and the last one wins, because a log a
    /// later edit replaced has been folded into a table and deleted. Keeping
    /// every number the manifest ever named would send the reader looking for
    /// files the manager removed months ago.
    /// </remarks>
    private sealed class LogNumbers
    {
        internal ulong? Current { get; set; }

        internal ulong? Previous { get; set; }
    }

    private static void ReadEdit(
        byte[] record,
        string named,
        Dictionary<ulong, int> live,
        LogNumbers logs,
        ref string? comparator)
    {
        var at = 0;
        var span = record.AsSpan();

        while (at < span.Length)
        {
            var tag = VarInt.Read(span, ref at, $"a version edit in '{named}'");

            switch (tag)
            {
                case ComparatorTag:
                    comparator = Encoding.UTF8.GetString(Bytes(span, ref at, named));
                    break;
                case LogNumberTag:
                    logs.Current = VarInt.Read(span, ref at, $"a log number in '{named}'");
                    break;
                case PreviousLogNumberTag:
                    logs.Previous = VarInt.Read(span, ref at, $"a log number in '{named}'");
                    break;
                case NextFileNumberTag:
                case LastSequenceTag:
                    VarInt.Read(span, ref at, $"a counter in '{named}'");
                    break;
                case CompactPointerTag:
                    VarInt.Read(span, ref at, $"a level in '{named}'");
                    Bytes(span, ref at, named);
                    break;
                case DeletedFileTag:
                    VarInt.Read(span, ref at, $"a level in '{named}'");
                    live.Remove(VarInt.Read(span, ref at, $"a file number in '{named}'"));
                    break;
                case NewFileTag:
                    var level = (int)VarInt.Read(span, ref at, $"a level in '{named}'");
                    live[VarInt.Read(span, ref at, $"a file number in '{named}'")] = level;
                    VarInt.Read(span, ref at, $"a file size in '{named}'");
                    Bytes(span, ref at, named);
                    Bytes(span, ref at, named);
                    break;
                default:
                    throw new StateReadException(
                        $"'{named}' carries a version edit tagged {tag}, and this reader models "
                        + $"only {ComparatorTag}, {LogNumberTag}, {NextFileNumberTag}, "
                        + $"{LastSequenceTag}, {CompactPointerTag}, {DeletedFileTag}, "
                        + $"{NewFileTag} and {PreviousLogNumberTag}. An edit this reader cannot "
                        + "read may add or drop a file, so which files hold the state is no "
                        + "longer known - which is why this refuses rather than reading on.");
            }
        }
    }

    private static ReadOnlySpan<byte> Bytes(ReadOnlySpan<byte> span, ref int at, string named) =>
        DeclaredLength.Next(
            span,
            ref at,
            VarInt.ReadLength(span, ref at, $"a length in '{named}'"),
            $"a version edit in '{named}'",
            "a value");
}
