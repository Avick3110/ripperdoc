using System.Text;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The manager's own state, read from its files as bytes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This cannot write, and not because it is careful.</strong> It never
/// opens a database - it reads files, and every one of them through a single
/// site that opens for reading and creates nothing. A library that opened the
/// database instead would replay the write-ahead log and may compact, which is
/// a write to the state of a manager that may be running.
/// </para>
/// <para>
/// <strong>Keys are enumerated; values are not.</strong> A value's bytes are
/// copied only for a key under one of the prefixes the caller asked for. The
/// database also holds account credentials, and a reader that materialised
/// every value would be holding those whether or not it went on to look at
/// them.
/// </para>
/// <para>
/// Whether a manager was running while this was read is <strong>not
/// established</strong>, and <see cref="Caveats" /> says so on every reading
/// rather than leaving a caller to assume it was checked.
/// </para>
/// </remarks>
public sealed class StateDatabase
{
    private readonly Dictionary<string, byte[]> values;

    private StateDatabase(
        string directory,
        IReadOnlyList<string> filesRead,
        Dictionary<string, byte[]> values,
        int keysSeen,
        int keysLive,
        int entriesRead)
    {
        Directory = directory;
        FilesRead = filesRead;
        this.values = values;
        KeysSeen = keysSeen;
        KeysLive = keysLive;
        EntriesRead = entriesRead;
    }

    /// <summary>The directory this was read from.</summary>
    public string Directory { get; }

    /// <summary>Every file read, as the version manifest named them.</summary>
    public IReadOnlyList<string> FilesRead { get; }

    /// <summary>How many distinct keys the files hold.</summary>
    public int KeysSeen { get; }

    /// <summary>
    /// How many of those keys are set rather than deleted.
    /// </summary>
    /// <remarks>
    /// A key whose newest entry deletes it is absent, not present holding what
    /// it held before.
    /// </remarks>
    public int KeysLive { get; }

    /// <summary>How many entries were read across every file.</summary>
    public int EntriesRead { get; }

    /// <summary>
    /// The live values under the prefixes this reading was asked for.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> Values => values;

    /// <summary>
    /// What this reading did not establish about itself.
    /// </summary>
    /// <remarks>
    /// Carried on the reading rather than stated in prose somewhere else,
    /// because a caveat a caller cannot see is one that does not exist.
    /// </remarks>
    public IReadOnlyList<string> Caveats { get; } =
    [
        "whether the manager was running while this was read is not established: what a running "
        + "manager changes on disk that a stopped one does not has not been measured, so this "
        + "reading neither detected one nor ruled one out. It is a read of bytes either way - "
        + "nothing here opens a database or writes anything.",
    ];

    /// <summary>
    /// Reads a manager's state directory, or reports that there is none.
    /// </summary>
    /// <param name="directory">The state directory.</param>
    /// <param name="prefixes">
    /// The key prefixes whose values are wanted. Keys outside them are counted
    /// and their values never read.
    /// </param>
    /// <returns>The state, or null where the directory holds no database.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="StateReadException">
    /// The directory holds a database this reader does not model, or one it
    /// could not read.
    /// </exception>
    public static StateDatabase? In(string directory, IReadOnlyList<string> prefixes)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(prefixes);

        var version = StateVersion.In(directory);

        if (version is null)
        {
            return null;
        }

        var wanted = prefixes.ToArray();
        var newest = new Dictionary<string, (ulong Sequence, bool IsValue, byte[]? Value)>(
            StringComparer.Ordinal);
        var entries = 0;

        void Keep(ReadOnlySpan<byte> key, ulong sequence, bool isValue, ReadOnlySpan<byte> value)
        {
            entries++;

            var name = Encoding.UTF8.GetString(key);

            if (newest.TryGetValue(name, out var held) && held.Sequence >= sequence)
            {
                return;
            }

            // The one place a value's bytes leave the buffer, and it is guarded
            // by the prefix test rather than by a caller remembering not to look.
            var keep = isValue && Array.Exists(wanted, prefix => name.StartsWith(prefix, StringComparison.Ordinal));

            newest[name] = (sequence, isValue, keep ? value.ToArray() : null);
        }

        foreach (var table in version.Tables)
        {
            TableFile.ReadInto(version.Read(table), Path.GetFileName(table), Keep);
        }

        foreach (var log in version.Logs)
        {
            var name = Path.GetFileName(log);

            foreach (var record in LogRecords.In(version.Read(log), name))
            {
                WriteBatch.ReadInto(record, name, Keep);
            }
        }

        return new StateDatabase(
            directory,
            [.. version.Tables.Concat(version.Logs).Select(Path.GetFileName).OfType<string>()],
            newest.Where(pair => pair.Value.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value!, StringComparer.Ordinal),
            newest.Count,
            newest.Count(pair => pair.Value.IsValue),
            entries);
    }

    /// <summary>
    /// The text one key holds, or null where the key is absent.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Its value as text, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key" /> is null.</exception>
    public string? Text(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return values.TryGetValue(key, out var value) ? Encoding.UTF8.GetString(value) : null;
    }

    /// <summary>
    /// Every key under a prefix, in the order the format sorts them.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix" /> is null.</exception>
    public IEnumerable<string> KeysUnder(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return values.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }
}
