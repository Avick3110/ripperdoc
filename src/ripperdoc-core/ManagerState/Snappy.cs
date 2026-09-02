namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The one compression the on-disk format uses for a table's blocks.
/// </summary>
/// <remarks>
/// Decompression only. Nothing in this engine writes to a manager's state, so
/// there is no compressor here and no code path that could grow into one.
/// </remarks>
internal static class Snappy
{
    /// <summary>
    /// Decompresses one block.
    /// </summary>
    /// <param name="source">The compressed bytes.</param>
    /// <param name="what">What is being read, for a refusal.</param>
    /// <returns>The bytes it stands for.</returns>
    /// <exception cref="StateReadException">
    /// The stream is truncated, names an offset behind its own start, or does
    /// not produce the length it declares.
    /// </exception>
    internal static byte[] Decompress(ReadOnlySpan<byte> source, string what)
    {
        var at = 0;
        var declared = VarInt.ReadLength(source, ref at, $"{what}'s compressed length preamble");
        var output = new byte[declared];
        var written = 0;

        while (at < source.Length)
        {
            var tag = source[at++];

            if ((tag & 3) == 0)
            {
                var length = tag >> 2;

                if (length >= 60)
                {
                    var extra = length - 59;
                    length = (int)ReadLittleEndian(source, ref at, extra, what);
                }

                length += 1;
                Take(source, ref at, length, what);
                Room(written, length, declared, what);
                source.Slice(at - length, length).CopyTo(output.AsSpan(written));
                written += length;
                continue;
            }

            int copyLength, offset;

            switch (tag & 3)
            {
                case 1:
                    copyLength = 4 + ((tag >> 2) & 7);
                    Take(source, ref at, 1, what);
                    offset = ((tag >> 5) << 8) | source[at - 1];
                    break;
                case 2:
                    copyLength = (tag >> 2) + 1;
                    offset = (int)ReadLittleEndian(source, ref at, 2, what);
                    break;
                default:
                    copyLength = (tag >> 2) + 1;
                    offset = (int)ReadLittleEndian(source, ref at, 4, what);
                    break;
            }

            if (offset <= 0 || offset > written)
            {
                throw new StateReadException(
                    $"{what} carries a compressed reference {offset} bytes back from a point only "
                    + $"{written} bytes in, so it names bytes that do not exist. The block is "
                    + "corrupt or is not compressed the way this reader models.");
            }

            Room(written, copyLength, declared, what);

            // Byte at a time, because the format allows a reference shorter than
            // the run it produces and that run is built out of what it just wrote.
            for (var i = 0; i < copyLength; i++)
            {
                output[written] = output[written - offset];
                written++;
            }
        }

        return written == declared
            ? output
            : throw new StateReadException(
                $"{what} declares {declared} decompressed bytes and produced {written}, so the "
                + "block is truncated or is not compressed the way this reader models.");
    }

    private static void Take(ReadOnlySpan<byte> source, ref int at, int count, string what)
    {
        if (at + count > source.Length)
        {
            throw new StateReadException(
                $"{what} ends part-way through a compressed run of {count} bytes, so the block is "
                + "truncated or is not compressed the way this reader models.");
        }

        at += count;
    }

    private static void Room(int written, int length, int declared, string what)
    {
        if (written + length > declared)
        {
            throw new StateReadException(
                $"{what} produces more than the {declared} decompressed bytes it declares, so the "
                + "block is corrupt or is not compressed the way this reader models.");
        }
    }

    private static ulong ReadLittleEndian(
        ReadOnlySpan<byte> source, ref int at, int count, string what)
    {
        Take(source, ref at, count, what);

        ulong value = 0;

        for (var i = 0; i < count; i++)
        {
            value |= (ulong)source[at - count + i] << (i * 8);
        }

        return value;
    }
}
