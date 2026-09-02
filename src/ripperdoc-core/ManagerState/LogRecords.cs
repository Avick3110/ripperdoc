namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The framing both the write-ahead log and the version manifest are written
/// in: fixed-size blocks holding checksummed records, one record split across
/// several when it does not fit.
/// </summary>
internal static class LogRecords
{
    private const int BlockSize = 32768;
    private const int HeaderSize = 7;

    private const byte Padding = 0;
    private const byte Whole = 1;
    private const byte First = 2;
    private const byte Middle = 3;
    private const byte Last = 4;

    /// <summary>
    /// Every record in a file, each reassembled from the fragments that carry
    /// it.
    /// </summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="what">The file, for a refusal.</param>
    /// <returns>The records.</returns>
    /// <exception cref="StateReadException">
    /// A record type is outside the modelled set, a checksum disagrees, a
    /// record's declared length runs past the file, or fragments arrive in an
    /// order that carries no record.
    /// </exception>
    internal static IReadOnlyList<byte[]> In(byte[] data, string what)
    {
        var records = new List<byte[]>();
        var partial = new List<byte>();
        var assembling = false;
        var at = 0;

        while (at + HeaderSize <= data.Length)
        {
            if (BlockSize - (at % BlockSize) < HeaderSize)
            {
                at = ((at / BlockSize) + 1) * BlockSize;
                continue;
            }

            var stored = BitConverter.ToUInt32(data, at);
            var length = BitConverter.ToUInt16(data, at + 4);
            var kind = data[at + 6];

            if (kind == Padding)
            {
                at = ((at + BlockSize - 1) / BlockSize) * BlockSize;
                continue;
            }

            if (kind is not (Whole or First or Middle or Last))
            {
                throw new StateReadException(
                    $"'{what}' holds a record of type {kind} at byte {at}, and this reader models "
                    + $"only {Padding} (padding), {Whole} (whole), {First}, {Middle} and {Last} "
                    + "(the fragments of one record). A type outside those is a format this "
                    + "reader has not been shown - report it rather than reading past it.");
            }

            if (at + HeaderSize + length > data.Length)
            {
                throw new StateReadException(
                    $"'{what}' holds a record at byte {at} declaring {length} bytes, which runs "
                    + "past the end of the file. It is truncated, or a write was interrupted.");
            }

            var payload = data.AsSpan(at + HeaderSize, length);

            if (Checksum.Unmask(stored) != Checksum.Of(kind, payload))
            {
                throw new StateReadException(
                    $"'{what}' holds a record at byte {at} whose checksum does not match its own "
                    + "bytes, so the file has been damaged since it was written. Nothing here can "
                    + "say what it was meant to hold; the manager rewrites this file when it next "
                    + "runs.");
            }

            at += HeaderSize + length;

            switch (kind)
            {
                case Whole:
                    Idle(assembling, what, kind);
                    records.Add(payload.ToArray());
                    break;
                case First:
                    Idle(assembling, what, kind);
                    partial.Clear();
                    partial.AddRange(payload);
                    assembling = true;
                    break;
                case Middle:
                    Started(assembling, what, kind);
                    partial.AddRange(payload);
                    break;
                default:
                    Started(assembling, what, kind);
                    partial.AddRange(payload);
                    records.Add([.. partial]);
                    partial.Clear();
                    assembling = false;
                    break;
            }
        }

        return assembling
            ? throw new StateReadException(
                $"'{what}' ends part-way through a record: a first or middle fragment arrived and "
                + "the last one never did. The file is truncated, or a write was interrupted.")
            : records;
    }

    private static void Idle(bool assembling, string what, byte kind)
    {
        if (assembling)
        {
            throw new StateReadException(
                $"'{what}' begins a record of type {kind} while an earlier one is still waiting "
                + "for its last fragment, so the two cannot both be what the file says they are. "
                + "The file is damaged.");
        }
    }

    private static void Started(bool assembling, string what, byte kind)
    {
        if (!assembling)
        {
            throw new StateReadException(
                $"'{what}' carries a fragment of type {kind} with no record open for it to "
                + "continue, so the fragment it belongs to is missing. The file is damaged.");
        }
    }
}
