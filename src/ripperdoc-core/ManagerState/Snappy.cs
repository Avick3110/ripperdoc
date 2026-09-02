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
    private const int MostACopyProduces = 64;
    private const int FewestBytesOfACopyProducingThat = 3;

    /// <summary>
    /// Decompresses one block.
    /// </summary>
    /// <param name="source">The compressed bytes.</param>
    /// <param name="what">What is being read, for a refusal.</param>
    /// <returns>The bytes it stands for.</returns>
    /// <exception cref="StateReadException">
    /// The stream is truncated, declares more than its bytes can produce, names
    /// an offset behind its own start, or does not produce the length it
    /// declares.
    /// </exception>
    internal static byte[] Decompress(ReadOnlySpan<byte> source, string what)
    {
        var at = 0;
        var declared = VarInt.ReadLength(source, ref at, $"{what}'s compressed length preamble");
        var ceiling = Ceiling(source.Length - at);

        // The preamble is the block's own word for how much it holds, and a
        // block is allocated on that word. The format bounds it: a copy
        // producing up to 64 bytes takes at least 3 (the two-byte copy
        // produces at most 11), and a literal never produces more than it
        // consumes - so the compressed bytes cap what they can stand for.
        if (declared > ceiling)
        {
            throw new StateReadException(
                $"{what} declares {declared} decompressed bytes, and its {source.Length - at} "
                + $"compressed bytes can produce at most {ceiling}. The preamble is corrupt, or "
                + "the block is not compressed the way this reader models.");
        }

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
                    var declaredRun = ReadLittleEndian(
                        source, ref at, extra, what, "a literal run's length");

                    if (declaredRun >= int.MaxValue)
                    {
                        throw new StateReadException(
                            $"{what} declares a literal run of {declaredRun} bytes, which is "
                            + "larger than anything this reader can hold. The block is corrupt "
                            + "or is not compressed the way this reader models.");
                    }

                    length = (int)declaredRun;
                }

                length += 1;
                var literal = DeclaredLength.Next(source, ref at, length, what, "a literal run");
                Room(written, length, declared, what);
                literal.CopyTo(DeclaredLength.At(output, written, length, what, "a literal run"));
                written += length;
                continue;
            }

            int copyLength, offset;

            switch (tag & 3)
            {
                case 1:
                    copyLength = 4 + ((tag >> 2) & 7);
                    offset = ((tag >> 5) << 8) | DeclaredLength.Next(source, ref at, 1, what, "a copy offset")[0];
                    break;
                case 2:
                    copyLength = (tag >> 2) + 1;
                    offset = (int)ReadLittleEndian(source, ref at, 2, what, "a copy offset");
                    break;
                default:
                    copyLength = (tag >> 2) + 1;
                    offset = (int)ReadLittleEndian(source, ref at, 4, what, "a copy offset");
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

    private static long Ceiling(int compressed) =>
        (long)MostACopyProduces
        * ((compressed + FewestBytesOfACopyProducingThat - 1) / FewestBytesOfACopyProducingThat);

    private static void Room(int written, int length, int declared, string what)
    {
        if ((long)written + length > declared)
        {
            throw new StateReadException(
                $"{what} produces more than the {declared} decompressed bytes it declares, so the "
                + "block is corrupt or is not compressed the way this reader models.");
        }
    }

    private static ulong ReadLittleEndian(
        ReadOnlySpan<byte> source, ref int at, int count, string what, string of)
    {
        var bytes = DeclaredLength.Next(source, ref at, count, what, of);
        ulong value = 0;

        for (var i = 0; i < count; i++)
        {
            value |= (ulong)bytes[i] << (i * 8);
        }

        return value;
    }
}
