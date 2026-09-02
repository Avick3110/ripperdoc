namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The one place a length the file declares is held against the bytes that
/// are actually there.
/// </summary>
/// <remarks>
/// <para>
/// Every construct in this format is a declared length at an offset, and a
/// site that adds the two in the width of an <see langword="int" /> lets a
/// length near the signed maximum wrap the sum negative, pass the guard, and
/// reach the slice - which raises the platform's own exception rather than
/// this reader's refusal. The sum is taken here in a width that holds it, so
/// that no site has to remember to.
/// </para>
/// <para>
/// One site, so that "every declared length is refused by name" is a property
/// of the code rather than a habit. Nothing else in this namespace slices a
/// buffer by a computed length; a check holds the source to that.
/// </para>
/// </remarks>
internal static class DeclaredLength
{
    /// <summary>
    /// The bytes a declared length names at an offset, or a refusal by name.
    /// </summary>
    /// <param name="buffer">The bytes the length was declared within.</param>
    /// <param name="at">Where the named bytes begin.</param>
    /// <param name="length">How many bytes are declared.</param>
    /// <param name="what">What declared the length, for a refusal.</param>
    /// <param name="of">What the bytes are, for a refusal.</param>
    /// <returns>The named bytes.</returns>
    /// <exception cref="StateReadException">
    /// The offset or the length is negative, or the two together run past the
    /// end of the buffer.
    /// </exception>
    internal static ReadOnlySpan<byte> At(
        ReadOnlySpan<byte> buffer, int at, long length, string what, string of)
    {
        Check(buffer.Length, at, length, what, of);

        return buffer.Slice(at, (int)length);
    }

    /// <inheritdoc cref="At(ReadOnlySpan{byte}, int, long, string, string)" />
    internal static Span<byte> At(Span<byte> buffer, int at, long length, string what, string of)
    {
        Check(buffer.Length, at, length, what, of);

        return buffer.Slice(at, (int)length);
    }

    /// <summary>
    /// The bytes a declared length names at an offset, kept as memory so that
    /// an iterator can hand them on.
    /// </summary>
    /// <inheritdoc cref="At(ReadOnlySpan{byte}, int, long, string, string)" />
    internal static ReadOnlyMemory<byte> Memory(
        byte[] buffer, int at, long length, string what, string of)
    {
        Check(buffer.Length, at, length, what, of);

        return buffer.AsMemory(at, (int)length);
    }

    /// <summary>
    /// The bytes a declared length names at a cursor, advancing the cursor past
    /// them.
    /// </summary>
    /// <param name="buffer">The bytes the length was declared within.</param>
    /// <param name="at">Where the named bytes begin; advanced past them.</param>
    /// <param name="length">How many bytes are declared.</param>
    /// <param name="what">What declared the length, for a refusal.</param>
    /// <param name="of">What the bytes are, for a refusal.</param>
    /// <returns>The named bytes.</returns>
    /// <exception cref="StateReadException">
    /// The offset or the length is negative, or the two together run past the
    /// end of the buffer.
    /// </exception>
    internal static ReadOnlySpan<byte> Next(
        ReadOnlySpan<byte> buffer, ref int at, long length, string what, string of)
    {
        var taken = At(buffer, at, length, what, of);
        at += taken.Length;

        return taken;
    }

    /// <inheritdoc cref="Next(ReadOnlySpan{byte}, ref int, long, string, string)" />
    internal static Span<byte> Next(
        Span<byte> buffer, ref int at, long length, string what, string of)
    {
        var taken = At(buffer, at, length, what, of);
        at += taken.Length;

        return taken;
    }

    private static void Check(int available, int at, long length, string what, string of)
    {
        if (at < 0 || length < 0 || at + length > available)
        {
            throw new StateReadException(
                $"{what} names {of} of {length} bytes at byte {at}, which runs past the end of "
                + $"the {available} bytes there are. The file is truncated, or is not the format "
                + "this reader models.");
        }
    }
}
