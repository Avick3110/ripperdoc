namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The checksum the on-disk format stores beside every record and every block.
/// </summary>
/// <remarks>
/// CRC32C, and the format stores it rotated and offset rather than bare, so
/// that a stored checksum cannot be mistaken for the checksum of the bytes that
/// hold it.
/// </remarks>
internal static class Checksum
{
    private const uint Polynomial = 0x82F63B78;
    private const uint MaskDelta = 0xA282EAD8;

    private static readonly uint[] Table = BuildTable();

    /// <summary>
    /// The checksum of some bytes.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The checksum.</returns>
    internal static uint Of(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    /// The checksum of one leading byte and then some bytes.
    /// </summary>
    /// <param name="first">The leading byte.</param>
    /// <param name="rest">The bytes after it.</param>
    /// <returns>The checksum.</returns>
    /// <remarks>
    /// A record's checksum covers its type byte and its payload, which are not
    /// contiguous in the buffer the payload is read out of.
    /// </remarks>
    internal static uint Of(byte first, ReadOnlySpan<byte> rest)
    {
        var crc = (0xFFFFFFFFu >> 8) ^ Table[(0xFFFFFFFFu ^ first) & 0xFF];

        foreach (var b in rest)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    /// The checksum a stored value stands for.
    /// </summary>
    /// <param name="stored">The value as the file holds it.</param>
    /// <returns>The checksum to compare against.</returns>
    internal static uint Unmask(uint stored)
    {
        var rotated = stored - MaskDelta;

        return (rotated >> 17) | (rotated << 15);
    }

    /// <summary>
    /// The stored form of a checksum.
    /// </summary>
    /// <param name="crc">The checksum.</param>
    /// <returns>The value to store.</returns>
    internal static uint Mask(uint crc) => ((crc >> 15) | (crc << 17)) + MaskDelta;

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (var i = 0u; i < table.Length; i++)
        {
            var value = i;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ Polynomial : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
