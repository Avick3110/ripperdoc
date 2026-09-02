namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// One record of the write-ahead log: a run of changes written together, under
/// the sequence number they were written at.
/// </summary>
internal static class WriteBatch
{
    private const int HeaderSize = 12;

    /// <summary>
    /// Offers every change one record carries.
    /// </summary>
    /// <param name="record">The record's bytes.</param>
    /// <param name="what">The file it came from, for a refusal.</param>
    /// <param name="sink">What to offer each change to.</param>
    /// <exception cref="StateReadException">
    /// The record is shorter than its own header, carries fewer changes than it
    /// declares, holds a change of a kind this reader does not model, or
    /// declares a key or a value running past the record's own end.
    /// </exception>
    internal static void ReadInto(byte[] record, string what, StateEntrySink sink)
    {
        if (record.Length < HeaderSize)
        {
            throw new StateReadException(
                $"'{what}' holds a record of {record.Length} bytes, and every batch of this "
                + $"format opens with {HeaderSize}. The record is truncated.");
        }

        var span = record.AsSpan();
        var sequence = BitConverter.ToUInt64(span);
        var declared = BitConverter.ToUInt32(span[8..]);
        var at = HeaderSize;

        for (var written = 0u; written < declared; written++)
        {
            if (at >= span.Length)
            {
                throw new StateReadException(
                    $"'{what}' holds a batch declaring {declared} changes and carrying {written}. "
                    + "The record is truncated, so what it was meant to set is not known - and "
                    + "the keys it would have set read as keys the manager never wrote.");
            }

            var isValue = TableFile.IsValue(span[at++], what);
            var key = DeclaredLength.Next(
                span,
                ref at,
                VarInt.ReadLength(span, ref at, $"a key length in '{what}'"),
                $"a batch in '{what}'",
                "a key");
            var value = isValue
                ? DeclaredLength.Next(
                    span,
                    ref at,
                    VarInt.ReadLength(span, ref at, $"a value length in '{what}'"),
                    $"a batch in '{what}'",
                    "a value")
                : default;

            sink(key, sequence + written, isValue, value);
        }
    }
}
