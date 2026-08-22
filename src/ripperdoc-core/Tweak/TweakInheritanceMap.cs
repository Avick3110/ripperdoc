namespace Ripperdoc.Core.Tweak;

/// <summary>
/// Which shipped records were cloned from which, as the framework records it.
/// </summary>
/// <remarks>
/// <para>
/// The shipped database has no inheritance in it. A record cloned from another
/// when the game's data was built arrives as a flat copy, and by the time it
/// ships nothing says where its values came from. The framework carries that
/// lost structure as its own metadata file and loads it before it reads a
/// single tweak, because it needs it to decide where a write should land.
/// </para>
/// <para>
/// This matters to a resolved state because a value set on one record can move
/// values on records nobody named. Without this map that movement is invisible,
/// and a report over a layer that contains one would say a value is untouched
/// while the game changes it.
/// </para>
/// </remarks>
public sealed class TweakInheritanceMap
{
    private readonly IReadOnlyDictionary<ulong, IReadOnlyList<ulong>> _descendants;

    private TweakInheritanceMap(
        IReadOnlyDictionary<ulong, IReadOnlyList<ulong>> descendants,
        string description,
        int edgeCount,
        bool wasRead)
    {
        _descendants = descendants;
        Description = description;
        EdgeCount = edgeCount;
        WasRead = wasRead;
    }

    /// <summary>
    /// A map that records nothing, for a replay with no framework metadata to
    /// consult.
    /// </summary>
    /// <remarks>
    /// Named rather than represented by a null, so that going without is a
    /// choice a caller made. What follows is that a value moving through
    /// inheritance cannot be seen, and the replay says so rather than reporting
    /// that none did.
    /// </remarks>
    public static TweakInheritanceMap None { get; } = new(
        new Dictionary<ulong, IReadOnlyList<ulong>>(),
        "no inheritance metadata",
        0,
        wasRead: false);

    /// <summary>What was read, in words fit for a provenance block.</summary>
    public string Description { get; }

    /// <summary>How many records have a recorded source in total.</summary>
    public int EdgeCount { get; }

    /// <summary>How many records have descendants.</summary>
    public int BaseCount => _descendants.Count;

    /// <summary>
    /// Whether a map was supplied at all.
    /// </summary>
    /// <remarks>
    /// This is not "has edges". A metadata file that reads cleanly and declares
    /// no descendants is a map that was consulted and found nothing, which is a
    /// different fact from no map having been given - and collapsing the two
    /// would have a replay announce that it did not look at a file it had just
    /// read.
    /// </remarks>
    public bool WasRead { get; }

    /// <summary>
    /// Read the framework's inheritance metadata.
    /// </summary>
    /// <param name="path">The metadata file.</param>
    /// <returns>The map it declares.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">There is no file at that path.</exception>
    /// <exception cref="InvalidDataException">The file is not readable metadata.</exception>
    public static TweakInheritanceMap OpenReadOnly(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("There is no inheritance metadata at this path.", path);
        }

        var bytes = File.ReadAllBytes(path);
        var descendants = new Dictionary<ulong, IReadOnlyList<ulong>>();
        var offset = 0;

        try
        {
            var entries = ReadUInt64(bytes, ref offset);

            for (var entry = 0UL; entry < entries; entry++)
            {
                var recordId = ReadUInt64(bytes, ref offset);
                var count = ReadUInt64(bytes, ref offset);

                // Checked against what is left to read before anything is
                // reserved. The count comes out of a file that may be corrupt
                // and it sizes an allocation, so a count larger than the file
                // could satisfy is the file ending early - and allocating on it
                // first raises something this method does not promise, or
                // exhausts memory before it can raise anything.
                if (count > (ulong)(bytes.Length - offset) / sizeof(ulong))
                {
                    throw new InvalidDataException(
                        $"'{Path.GetFileName(path)}' could not be read as inheritance metadata: it declares "
                        + $"{count} records cloned from one source with {bytes.Length - offset} bytes left to "
                        + "read them from.");
                }

                var children = new ulong[count];

                for (var child = 0UL; child < count; child++)
                {
                    children[child] = ReadUInt64(bytes, ref offset);
                }

                // Merged rather than replaced, because the framework adds each
                // entry to whatever it already holds for that source. A source
                // named twice contributes both sets, and replacing would lose
                // records whose values really do move.
                if (descendants.TryGetValue(recordId, out var already))
                {
                    descendants[recordId] = already.Concat(children).Distinct().ToArray();
                }
                else
                {
                    descendants[recordId] = children;
                }
            }
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' could not be read as inheritance metadata: it ends before the "
                + "structure it declares does.",
                exception);
        }

        // A file that parses and leaves a tail was not understood. Read as a
        // success it would silently drop whatever the tail declared, and every
        // value moving through those records would then be invisible.
        if (offset != bytes.Length)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' could not be read as inheritance metadata: {bytes.Length - offset} "
                + $"of {bytes.Length} bytes remain after the {descendants.Count} records it declares.");
        }

        var edges = descendants.Values.Sum(children => children.Count);

        return new TweakInheritanceMap(
            descendants,
            $"{Path.GetFileName(path)}, {edges} records cloned from {descendants.Count} "
            + (descendants.Count == 1 ? "source" : "sources"),
            edges,
            wasRead: true);
    }

    /// <summary>
    /// Build a map directly, for a caller that has the structure already.
    /// </summary>
    /// <param name="descendants">Each source record and what was cloned from it.</param>
    /// <param name="description">What this map came from.</param>
    /// <returns>The map.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TweakInheritanceMap Of(
        IReadOnlyDictionary<ulong, IReadOnlyList<ulong>> descendants,
        string description)
    {
        ArgumentNullException.ThrowIfNull(descendants);
        ArgumentNullException.ThrowIfNull(description);

        return new TweakInheritanceMap(
            descendants,
            description,
            descendants.Values.Sum(children => children.Count),
            wasRead: true);
    }

    /// <summary>Whether anything was cloned from this record.</summary>
    /// <param name="recordIdentifier">The record to ask about.</param>
    /// <returns>True if it has descendants.</returns>
    public bool IsSource(ulong recordIdentifier) => _descendants.ContainsKey(recordIdentifier);

    /// <summary>
    /// Everything cloned from this record.
    /// </summary>
    /// <param name="recordIdentifier">The record to ask about.</param>
    /// <returns>The descendants, or an empty list.</returns>
    public IReadOnlyList<ulong> Descendants(ulong recordIdentifier) =>
        _descendants.TryGetValue(recordIdentifier, out var children) ? children : [];

    private static ulong ReadUInt64(byte[] bytes, ref int offset)
    {
        var value = BitConverter.ToUInt64(bytes, offset);
        offset += sizeof(ulong);

        return value;
    }
}

/// <summary>
/// The values a database already holds, as the replay needs to ask about them.
/// </summary>
/// <remarks>
/// The replay needs two questions answered about the state before any tweak was
/// applied: whether something is there, and whether two values are the same.
/// The second is what decides whether a record that was cloned from another
/// still agrees with it - which is how the framework tells "this was never
/// overridden" from "this was", and therefore whether a write to the source
/// should move the copy.
/// </remarks>
public interface ITweakValueSource
{
    /// <summary>What was read, in words fit for a provenance block.</summary>
    string Description { get; }

    /// <summary>Whether the database holds a record under this identifier.</summary>
    /// <param name="identifier">The identifier to look up.</param>
    /// <returns>True if the record is there.</returns>
    bool HoldsRecord(ulong identifier);

    /// <summary>Whether the database holds a value under this identifier.</summary>
    /// <param name="identifier">The identifier to look up.</param>
    /// <returns>True if the value is there.</returns>
    bool HoldsValue(ulong identifier);

    /// <summary>
    /// Whether two values the database holds are the same value.
    /// </summary>
    /// <param name="left">One identifier.</param>
    /// <param name="right">The other.</param>
    /// <returns>
    /// True only where both are present and equal. A pair where either is
    /// absent is not equal, because an absent value agrees with nothing.
    /// </returns>
    bool ValuesMatch(ulong left, ulong right);
}
