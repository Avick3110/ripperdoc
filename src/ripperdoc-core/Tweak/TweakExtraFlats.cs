using Ripperdoc.Core.Schema;

namespace Ripperdoc.Core.Tweak;

/// <summary>
/// The properties the tweak framework adds to record types on top of the ones
/// the type model declares.
/// </summary>
/// <remarks>
/// <para>
/// The type model is not the whole property set a tweak file may write. The
/// framework ships its own metadata naming further properties per record type,
/// loads it before it reads a single tweak, and accepts writes to those names.
/// A reading that checks tweak files against the type model alone therefore
/// reports properties as unknown that the game accepts perfectly well - and
/// does so in bulk, because the additions cluster on exactly the record types
/// mods edit most.
/// </para>
/// <para>
/// This is a second machine-complete source rather than a list maintained here.
/// Nothing in this file names a record type or a property: it reads what the
/// framework shipped, and a type absent from it simply has no additions.
/// </para>
/// </remarks>
public sealed class TweakExtraFlats
{
    /// <summary>
    /// The fewest bytes one addition can occupy - a length byte, at least one
    /// name byte, and the two type hashes.
    /// </summary>
    private const int SmallestFlatSize = 1 + 1 + sizeof(ulong) + sizeof(ulong);

    private readonly IReadOnlyDictionary<ulong, IReadOnlyList<string>> _byTypeName;

    private TweakExtraFlats(
        IReadOnlyDictionary<ulong, IReadOnlyList<string>> byTypeName,
        string description,
        int typeCount,
        int propertyCount)
    {
        _byTypeName = byTypeName;
        Description = description;
        TypeCount = typeCount;
        PropertyCount = propertyCount;
    }

    /// <summary>
    /// A source that adds nothing, for a reading with no framework metadata to
    /// consult.
    /// </summary>
    /// <remarks>
    /// Named rather than represented by a null, so that a caller choosing to go
    /// without has chosen it. What follows from choosing it is that unknown
    /// properties cannot be counted honestly, and the reading says so instead of
    /// reporting a number that would be mostly wrong.
    /// </remarks>
    public static TweakExtraFlats None { get; } = new(
        new Dictionary<ulong, IReadOnlyList<string>>(),
        "no framework metadata",
        0,
        0);

    /// <summary>What was read, in words fit for a provenance block.</summary>
    public string Description { get; }

    /// <summary>How many record types carry additions.</summary>
    public int TypeCount { get; }

    /// <summary>How many additions there are in total.</summary>
    public int PropertyCount { get; }

    /// <summary>Whether this source has anything to add.</summary>
    public bool IsPresent => PropertyCount > 0;

    /// <summary>
    /// Read the framework's metadata file.
    /// </summary>
    /// <param name="path">The metadata file.</param>
    /// <returns>The additions it declares.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">There is no file at that path.</exception>
    /// <exception cref="InvalidDataException">The file is not readable metadata.</exception>
    public static TweakExtraFlats OpenReadOnly(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("There is no framework metadata at this path.", path);
        }

        var bytes = File.ReadAllBytes(path);
        var byTypeName = new Dictionary<ulong, List<string>>();
        var offset = 0;

        try
        {
            var typeCount = ReadUInt64(bytes, ref offset);

            for (var type = 0UL; type < typeCount; type++)
            {
                var typeName = ReadUInt64(bytes, ref offset);
                var flatCount = ReadUInt64(bytes, ref offset);

                // The count is a number out of a file that may be corrupt, and
                // it is used to size an allocation. Checked against what is left
                // to read before anything is reserved: a count larger than the
                // file could possibly satisfy is the file ending early, and
                // allocating on it first raises something this method does not
                // promise - or exhausts memory before it can raise anything.
                if (flatCount > (ulong)(bytes.Length - offset) / SmallestFlatSize)
                {
                    throw new InvalidDataException(
                        $"'{Path.GetFileName(path)}' could not be read as framework metadata: it declares "
                        + $"{flatCount} additions for one record type with {bytes.Length - offset} bytes left "
                        + "to read them from.");
                }

                var names = new List<string>((int)flatCount);

                for (var flat = 0UL; flat < flatCount; flat++)
                {
                    var length = bytes[offset++];
                    names.Add(System.Text.Encoding.ASCII.GetString(bytes, offset, length));
                    offset += length;

                    // The declared and the referenced type of each addition
                    // follow the name. Neither is needed to answer whether a
                    // property exists, so both are stepped over rather than
                    // read into a shape nothing consumes.
                    offset += 16;
                }

                // Merged rather than replaced. The framework appends each entry
                // to whatever it already holds for that record type, so a type
                // named twice contributes both sets - and replacing would drop
                // additions the game accepts while the count went on reporting
                // them.
                if (byTypeName.TryGetValue(typeName, out var already))
                {
                    names.InsertRange(0, already);
                }

                byTypeName[typeName] = names;
            }
        }
        // The three ways this walk can run off the end of the file each raise a
        // different type - an index past the array, a conversion with too few
        // bytes left, a string longer than what remains - and they all mean the
        // same thing about the file. Named individually rather than caught
        // wholesale, so a fault that is not "the file ended" still escapes.
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' could not be read as framework metadata: it ends before the "
                + "structure it declares does.",
                exception);
        }

        // A file that parses but leaves bytes behind was not understood. Read
        // as a success it would silently drop whatever the tail declared, and
        // every property in it would then be reported as one the schema lacks.
        if (offset != bytes.Length)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' could not be read as framework metadata: {bytes.Length - offset} "
                + $"of {bytes.Length} bytes remain after the {byTypeName.Count} record types it declares.");
        }

        var propertyCount = byTypeName.Values.Sum(names => names.Count);

        return new TweakExtraFlats(
            byTypeName.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value),
            $"{Path.GetFileName(path)}, {propertyCount} additions over {byTypeName.Count} record types",
            byTypeName.Count,
            propertyCount);
    }

    /// <summary>
    /// Whether the framework adds this property to this record type.
    /// </summary>
    /// <param name="canonicalTypeName">
    /// The record type, in the canonical spelling
    /// <see cref="RecordTypeNaming.Canonicalise"/> produces.
    /// </param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>True if the framework declares this addition.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public bool Declares(string canonicalTypeName, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(canonicalTypeName);
        ArgumentNullException.ThrowIfNull(propertyName);

        return _byTypeName.TryGetValue(NameHash(canonicalTypeName), out var names)
            && names.Contains(propertyName, StringComparer.Ordinal);
    }

    /// <summary>
    /// The hash the metadata keys a record type by.
    /// </summary>
    /// <param name="name">The name to hash.</param>
    /// <returns>The hash.</returns>
    /// <remarks>
    /// The metadata stores type names as the engine's own name hash rather than
    /// as text, so a lookup has to reproduce that arithmetic. It is the 64-bit
    /// Fowler-Noll-Vo variant over the name's bytes.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public static ulong NameHash(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        const ulong offsetBasis = 0xCBF29CE484222325UL;
        const ulong prime = 0x100000001B3UL;

        var hash = offsetBasis;
        foreach (var value in System.Text.Encoding.UTF8.GetBytes(name))
        {
            hash = (hash ^ value) * prime;
        }

        return hash;
    }

    private static ulong ReadUInt64(byte[] bytes, ref int offset)
    {
        var value = BitConverter.ToUInt64(bytes, offset);
        offset += sizeof(ulong);

        return value;
    }
}
