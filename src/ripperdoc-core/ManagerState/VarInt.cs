namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The base-128 varint the on-disk format writes its lengths and offsets as.
/// </summary>
internal static class VarInt
{
    private const int MaxBytes = 10;

    /// <summary>
    /// Reads one varint and advances past it.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="at">Where to read; advanced past what was read.</param>
    /// <param name="what">What is being read, for a refusal.</param>
    /// <returns>The value.</returns>
    /// <exception cref="StateReadException">
    /// The bytes end mid-varint, or the varint is longer than one can be.
    /// </exception>
    internal static ulong Read(ReadOnlySpan<byte> source, ref int at, string what)
    {
        ulong value = 0;

        for (var taken = 0; taken < MaxBytes; taken++)
        {
            if (at >= source.Length)
            {
                throw new StateReadException(
                    $"{what} ends part-way through a varint, so the length it carries cannot be "
                    + "read. The file is truncated or is not the format this reader models.");
            }

            var b = source[at++];
            value |= (ulong)(b & 0x7F) << (taken * 7);

            if ((b & 0x80) == 0)
            {
                return value;
            }
        }

        throw new StateReadException(
            $"{what} carries a varint longer than {MaxBytes} bytes, which no value in this format "
            + "can be. The file is corrupt or is not the format this reader models.");
    }

    /// <summary>
    /// Reads a varint that must fit a count of bytes in memory.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="at">Where to read; advanced past what was read.</param>
    /// <param name="what">What is being read, for a refusal.</param>
    /// <returns>The value.</returns>
    /// <exception cref="StateReadException">The value does not fit a length.</exception>
    internal static int ReadLength(ReadOnlySpan<byte> source, ref int at, string what)
    {
        var value = Read(source, ref at, what);

        return value <= int.MaxValue
            ? (int)value
            : throw new StateReadException(
                $"{what} declares a length of {value} bytes, which is larger than anything this "
                + "reader can hold. The file is corrupt or is not the format this reader models.");
    }
}
