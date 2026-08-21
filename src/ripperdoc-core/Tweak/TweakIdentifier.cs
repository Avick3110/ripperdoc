namespace Ripperdoc.Core.Tweak;

/// <summary>
/// The tweak database's own identifier arithmetic: the hash that turns a name
/// into the identifier the shipped database is keyed by.
/// </summary>
/// <remarks>
/// <para>
/// An identifier is <c>(byteLength &lt;&lt; 32) | crc32_iso_hdlc(name)</c>. The
/// checksum is incrementally appendable, which is the property that makes this
/// class an arbiter rather than a convenience: a field's identifier is
/// computable from its record's identifier plus the field name, so a schema
/// hypothesis can be checked against every shipped value without any
/// hash-to-name table existing anywhere.
/// </para>
/// <para>
/// Nothing here reads a file, a game install, or generated type information.
/// It is arithmetic over a string, which is why the check that validates every
/// schema claim survives in the mode that has no generated type information to
/// start from.
/// </para>
/// </remarks>
public static class TweakIdentifier
{
    /// <summary>
    /// The separator between a record name and one of its field names.
    /// </summary>
    public const char FieldSeparator = '.';

    /// <summary>
    /// The longest name an identifier can carry.
    /// </summary>
    /// <remarks>
    /// The length lives in eight bits of the identifier, so a longer name has
    /// no identifier at all - the value one would compute for it collides with
    /// a shorter name's and addresses the wrong thing.
    /// </remarks>
    public const int MaxNameLength = 255;

    /// <summary>
    /// The highest character an identifier can be computed over.
    /// </summary>
    /// <remarks>
    /// Names are hashed byte by byte. The pinned type model's own conversion
    /// replaces anything above this with a placeholder character, which gives
    /// two different names one identifier - so rather than reproduce a
    /// collision or guess at an encoding nothing here can check, a name
    /// carrying such a character is refused.
    /// </remarks>
    public const char MaxCharacter = (char)0x7F;

    private const uint ReversedPolynomial = 0xEDB88320u;

    private static readonly uint[] Table = BuildTable();

    /// <summary>
    /// The identifier of <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The full name, for example <c>Items.money</c>.</param>
    /// <returns>The identifier the database is keyed by.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is longer than <see cref="MaxNameLength"/>, or
    /// carries a character above <see cref="MaxCharacter"/>.
    /// </exception>
    public static ulong Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"A name of {name.Length} bytes has no identifier; the length field holds at most "
                + $"{MaxNameLength}.",
                nameof(name));
        }

        return ((ulong)name.Length << 32) | Checksum(0u, name);
    }

    /// <summary>
    /// The identifier of a field on a record, computed from the record's
    /// identifier alone.
    /// </summary>
    /// <param name="recordIdentifier">The owning record's identifier.</param>
    /// <param name="fieldName">The field's name, without the separator.</param>
    /// <returns>The identifier of <c>&lt;record&gt;.&lt;field&gt;</c>.</returns>
    /// <remarks>
    /// The record's name is never needed, and never reconstructed. Only its
    /// length and running checksum are, and both are carried in its identifier.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fieldName"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fieldName"/> is empty, or the pair has no identifier.
    /// Use <see cref="TryForField"/> wherever an unaddressable pair is data to
    /// record rather than a mistake to stop for.
    /// </exception>
    public static ulong ForField(ulong recordIdentifier, string fieldName)
    {
        if (!TryForField(recordIdentifier, fieldName, out var identifier))
        {
            throw new ArgumentException(
                $"'{fieldName}' has no identifier on this record: "
                + WhyUnaddressable(recordIdentifier, fieldName) + ".",
                nameof(fieldName));
        }

        return identifier;
    }

    /// <summary>
    /// The identifier of a field on a record, where one exists.
    /// </summary>
    /// <param name="recordIdentifier">The owning record's identifier.</param>
    /// <param name="fieldName">The field's name, without the separator.</param>
    /// <param name="identifier">
    /// The identifier of <c>&lt;record&gt;.&lt;field&gt;</c>, or zero if there is none.
    /// </param>
    /// <returns>False if this pair has no identifier at all.</returns>
    /// <remarks>
    /// <para>
    /// Total over its data. Every reason a pair can fail to have an identifier
    /// - a record identifier that is not well formed, a field name outside the
    /// range identifiers are defined over, a combined name longer than one can
    /// carry - comes back as false rather than as an exception, because each is
    /// a fact about the pair rather than a mistake by the caller. A sweep over
    /// millions of pairs has to be able to record one and carry on; losing every
    /// verdict already reached is not a safer outcome than recording the one
    /// that could not be reached.
    /// </para>
    /// <para>
    /// A null or empty field name is different in kind and still throws. No
    /// schema produces one, so it means the caller is wrong rather than the data.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fieldName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="fieldName"/> is empty.</exception>
    public static bool TryForField(ulong recordIdentifier, string fieldName, out ulong identifier)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        if (fieldName.Length == 0)
        {
            throw new ArgumentException("A field name cannot be empty.", nameof(fieldName));
        }

        identifier = 0;

        if (!IsWellFormed(recordIdentifier) || !IsWithinRange(fieldName))
        {
            return false;
        }

        var length = LengthOf(recordIdentifier) + 1 + fieldName.Length;
        if (length > MaxNameLength)
        {
            return false;
        }

        var checksum = Checksum(ChecksumOf(recordIdentifier), FieldSeparator.ToString());
        checksum = Checksum(checksum, fieldName);

        identifier = ((ulong)length << 32) | checksum;
        return true;
    }

    /// <summary>
    /// Whether every character of <paramref name="text"/> has a defined place in
    /// an identifier.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns>True if nothing in it is above <see cref="MaxCharacter"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static bool IsWithinRange(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var character in text)
        {
            if (character > MaxCharacter)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="identifier"/> is shaped like an identifier this
    /// arithmetic can build on.
    /// </summary>
    /// <param name="identifier">The identifier to test.</param>
    /// <returns>True if nothing is set above the length field.</returns>
    /// <remarks>
    /// The length occupies eight bits and nothing above them is ever set in a
    /// shipped database. A value with bits set there was built from a name too
    /// long to have an identifier, and reading its length would read the wrong
    /// eight bits - so it is refused rather than quietly used.
    /// </remarks>
    public static bool IsWellFormed(ulong identifier) => (identifier >> (32 + 8)) == 0;

    /// <summary>
    /// The name length an identifier carries in its high half.
    /// </summary>
    /// <param name="identifier">The identifier to read.</param>
    /// <returns>The length, in bytes, of the name this identifier was built from.</returns>
    public static int LengthOf(ulong identifier) => (int)((identifier >> 32) & 0xFFu);

    /// <summary>
    /// The checksum an identifier carries in its low half.
    /// </summary>
    /// <param name="identifier">The identifier to read.</param>
    /// <returns>The running checksum of the name this identifier was built from.</returns>
    public static uint ChecksumOf(ulong identifier) => (uint)(identifier & 0xFFFFFFFFu);

    /// <summary>
    /// CRC-32/ISO-HDLC of <paramref name="text"/>, continuing from
    /// <paramref name="previous"/>.
    /// </summary>
    /// <param name="previous">The running checksum to continue from; zero to start.</param>
    /// <param name="text">The text to append.</param>
    /// <returns>The checksum of everything hashed so far.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> carries a character above <see cref="MaxCharacter"/>.
    /// </exception>
    public static uint Checksum(uint previous, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var register = ~previous;
        foreach (var character in text)
        {
            if (character > MaxCharacter)
            {
                throw new ArgumentException(
                    $"'{character}' has no defined place in an identifier: the conversion this is checked "
                    + "against replaces it with a placeholder, so two different names would share one "
                    + "identifier.",
                    nameof(text));
            }

            register = Table[(register ^ (byte)character) & 0xFF] ^ (register >> 8);
        }

        return ~register;
    }

    // The message names the reason that actually applies rather than the one
    // that usually does. A caller told the wrong reason looks in the wrong
    // place, which costs more than saying nothing would.
    private static string WhyUnaddressable(ulong recordIdentifier, string fieldName)
    {
        if (!IsWellFormed(recordIdentifier))
        {
            return $"the record identifier 0x{recordIdentifier:X} has bits set above its length field, so the "
                + "length it appears to carry is not the length it was built from";
        }

        if (!IsWithinRange(fieldName))
        {
            return "the field name carries a character with no defined place in an identifier, and the "
                + "conversion this is checked against would replace it with a placeholder";
        }

        return $"the combined name would be {LengthOf(recordIdentifier) + 1 + fieldName.Length} bytes and the "
            + $"length field holds at most {MaxNameLength}";
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            var register = index;
            for (var bit = 0; bit < 8; bit++)
            {
                register = (register & 1) != 0
                    ? ReversedPolynomial ^ (register >> 1)
                    : register >> 1;
            }

            table[index] = register;
        }

        return table;
    }
}
