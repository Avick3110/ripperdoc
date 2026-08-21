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
        RequireAddressableLength(name.Length, nameof(name));

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
    /// <paramref name="fieldName"/> is empty, carries a character above
    /// <see cref="MaxCharacter"/>, or would make the combined name longer than
    /// <see cref="MaxNameLength"/>.
    /// </exception>
    public static ulong ForField(ulong recordIdentifier, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        if (fieldName.Length == 0)
        {
            throw new ArgumentException("A field name cannot be empty.", nameof(fieldName));
        }

        var checksum = ChecksumOf(recordIdentifier);
        var length = LengthOf(recordIdentifier) + 1 + fieldName.Length;
        RequireAddressableLength(length, nameof(fieldName));

        checksum = Checksum(checksum, FieldSeparator.ToString());
        checksum = Checksum(checksum, fieldName);

        return ((ulong)length << 32) | checksum;
    }

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

    private static void RequireAddressableLength(int length, string argumentName)
    {
        if (length > MaxNameLength)
        {
            throw new ArgumentException(
                $"A name of {length} bytes has no identifier; the length field holds at most {MaxNameLength}.",
                argumentName);
        }
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
