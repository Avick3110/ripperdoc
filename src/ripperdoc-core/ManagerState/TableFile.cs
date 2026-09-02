namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// A sorted table file - the form the state takes once the manager has folded
/// it out of the write-ahead log.
/// </summary>
internal static class TableFile
{
    /// <summary>What the last eight bytes of a table must be.</summary>
    internal const ulong Magic = 0xdb4775248b80fb57;

    /// <summary>How many bytes the footer takes.</summary>
    internal const int FooterSize = 48;

    /// <summary>The compression this reader models a block under: none.</summary>
    internal const byte Uncompressed = 0;

    /// <summary>The compression this reader models a block under: snappy.</summary>
    internal const byte SnappyCompressed = 1;

    private const int TrailerSize = 8;
    private const int BlockTrailerSize = 5;

    /// <summary>
    /// Offers every entry the table holds.
    /// </summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="what">The file, for a refusal.</param>
    /// <param name="sink">What to offer each entry to.</param>
    /// <exception cref="StateReadException">
    /// The footer, a block, a checksum or a compression byte is not what this
    /// reader models.
    /// </exception>
    internal static void ReadInto(byte[] data, string what, StateEntrySink sink)
    {
        if (data.Length < FooterSize)
        {
            throw new StateReadException(
                $"'{what}' is {data.Length} bytes and a table's footer alone is {FooterSize}, so "
                + "there is no table here to read. The file is truncated.");
        }

        var footer = DeclaredLength.At(
            data, data.Length - FooterSize, FooterSize, $"'{what}'", "a footer");

        if (BitConverter.ToUInt64(footer[^8..]) != Magic)
        {
            throw new StateReadException(
                $"'{what}' does not end with the eight bytes every table of this format ends "
                + $"with (0x{Magic:x16}). It is not a table this reader models - a different "
                + "table format, or a file that is not a table at all.");
        }

        var at = 0;
        VarInt.Read(footer, ref at, $"the metadata offset in '{what}'");
        VarInt.Read(footer, ref at, $"the metadata length in '{what}'");
        var indexOffset = VarInt.ReadLength(footer, ref at, $"the index offset in '{what}'");
        var indexLength = VarInt.ReadLength(footer, ref at, $"the index length in '{what}'");

        foreach (var (_, handle) in Entries(Block(data, indexOffset, indexLength, what), what))
        {
            var cursor = 0;
            var span = handle.Span;
            var offset = VarInt.ReadLength(span, ref cursor, $"a block offset in '{what}'");
            var length = VarInt.ReadLength(span, ref cursor, $"a block length in '{what}'");

            foreach (var (key, value) in Entries(Block(data, offset, length, what), what))
            {
                Offer(key.Span, value.Span, what, sink);
            }
        }
    }

    private static void Offer(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, string what, StateEntrySink sink)
    {
        if (key.Length < TrailerSize)
        {
            throw new StateReadException(
                $"'{what}' holds a key of {key.Length} bytes, and every key in a table of this "
                + $"format carries a {TrailerSize}-byte trailer saying when it was written. The "
                + "block is corrupt or is not the format this reader models.");
        }

        // Both halves of the key go through the primitive, so that the source
        // holds no range with a computed endpoint.
        var trailer = BitConverter.ToUInt64(DeclaredLength.At(
            key, key.Length - TrailerSize, TrailerSize, $"'{what}'", "a key's trailer"));
        var kind = (byte)(trailer & 0xFF);

        sink(
            DeclaredLength.At(key, 0, key.Length - TrailerSize, $"'{what}'", "a key"),
            trailer >> 8,
            IsValue(kind, what),
            value);
    }

    /// <summary>
    /// Whether an entry sets its key or deletes it.
    /// </summary>
    /// <param name="kind">The kind byte the file carries.</param>
    /// <param name="what">The file, for a refusal.</param>
    /// <returns>True where the entry sets a value.</returns>
    /// <exception cref="StateReadException">The kind is outside the modelled pair.</exception>
    internal static bool IsValue(byte kind, string what) => kind switch
    {
        0 => false,
        1 => true,
        _ => throw new StateReadException(
            $"'{what}' holds an entry of kind {kind}, and this reader models only 0 (the key is "
            + "deleted) and 1 (the key is set). An entry this reader cannot read is a key whose "
            + "state is unknown, and guessing at it is how a mod goes missing from a wanted set."),
    };

    private static byte[] Block(byte[] data, int offset, int length, string what)
    {
        var block = DeclaredLength.At(data, offset, length, $"'{what}'", "a block");
        var trailer = DeclaredLength.At(
            data, offset + length, BlockTrailerSize, $"'{what}'", "a block's trailer");
        var compression = trailer[0];
        var stored = BitConverter.ToUInt32(trailer[1..]);

        // A block's checksum covers its bytes and then the byte saying how they
        // are compressed, which sit together in the file.
        var covered = DeclaredLength.At(data, offset, length + 1, $"'{what}'", "a block");

        if (Checksum.Unmask(stored) != Checksum.Of(covered))
        {
            throw new StateReadException(
                $"'{what}' holds a block at byte {offset} whose checksum does not match its own "
                + "bytes, so the file has been damaged since it was written. Nothing here can say "
                + "what it was meant to hold.");
        }

        return compression switch
        {
            Uncompressed => block.ToArray(),
            // The subject places the decoder's own figures within the
            // compressed bytes, so a refusal carrying an offset of its own is
            // not read against the file.
            SnappyCompressed => Snappy.Decompress(
                block, $"the compressed form of the block at byte {offset} of '{what}'"),
            _ => throw new StateReadException(
                $"'{what}' holds a block at byte {offset} compressed by method {compression}, and "
                + $"this reader models only {Uncompressed} (none) and {SnappyCompressed} "
                + "(snappy). A block this reader cannot decompress is one whose keys are absent "
                + "from everything below, which reads as a mod the manager never knew about."),
        };
    }

    private static IEnumerable<(ReadOnlyMemory<byte> Key, ReadOnlyMemory<byte> Value)> Entries(
        byte[] block, string what)
    {
        if (block.Length < 4)
        {
            throw new StateReadException(
                $"'{what}' holds a block of {block.Length} bytes, too few to carry the count "
                + "every block of this format ends with. The file is corrupt.");
        }

        var restarts = BitConverter.ToUInt32(block, block.Length - 4);
        var limit = block.Length - 4 - ((long)restarts * 4);

        if (limit < 0)
        {
            throw new StateReadException(
                $"'{what}' holds a block declaring {restarts} restart points, which do not fit in "
                + $"its {block.Length} bytes. The file is corrupt.");
        }

        var at = 0;
        var previous = Array.Empty<byte>();

        while (at < limit)
        {
            var span = block.AsSpan();
            var shared = VarInt.ReadLength(span, ref at, $"a shared key length in '{what}'");
            var fresh = VarInt.ReadLength(span, ref at, $"a key length in '{what}'");
            var valueLength = VarInt.ReadLength(span, ref at, $"a value length in '{what}'");

            if (shared > previous.Length)
            {
                throw new StateReadException(
                    $"'{what}' holds an entry sharing {shared} bytes with a key of "
                    + $"{previous.Length}, so the key it stands for cannot be rebuilt. The block "
                    + "is corrupt.");
            }

            if ((long)at + fresh + valueLength > limit)
            {
                throw new StateReadException(
                    $"'{what}' holds an entry whose key and value run past the end of its block. "
                    + "The file is truncated or corrupt.");
            }

            var key = new byte[shared + fresh];
            DeclaredLength.At(previous, 0, shared, $"'{what}'", "a shared key prefix").CopyTo(key);
            DeclaredLength.Next(span, ref at, fresh, $"'{what}'", "a key")
                .CopyTo(DeclaredLength.At(key, shared, fresh, $"'{what}'", "a key"));

            yield return (key, DeclaredLength.Memory(block, at, valueLength, $"'{what}'", "a value"));

            at += valueLength;
            previous = key;
        }
    }
}
