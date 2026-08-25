using System.Globalization;

namespace Ripperdoc.Core.Archive;

/// <summary>
/// One resource carried by one archive.
/// </summary>
/// <param name="Hash">
/// The identifier the archive addresses this resource by. Always present: it is
/// what the container is keyed on, so there is no entry without one.
/// </param>
/// <param name="Name">
/// The resource path, if any naming source could supply one; otherwise
/// <see langword="null" />.
/// </param>
/// <param name="Size">The resource's size in bytes once unpacked.</param>
/// <param name="PackedSize">The resource's size in bytes as stored.</param>
/// <remarks>
/// A null <see cref="Name" /> is a reportable state, never a reason to drop the
/// entry. A reader that reports only the named ones silently omits the rest -
/// which would make an archive look smaller than it is and a contest look
/// absent when it is not.
/// </remarks>
public readonly record struct ArchiveEntry(ulong Hash, string? Name, uint Size, uint PackedSize)
{
    /// <summary>Whether a naming source supplied a path for this entry.</summary>
    public bool IsNamed => Name is not null;

    /// <summary>
    /// How this entry is written when it is reported.
    /// </summary>
    /// <remarks>
    /// This is the property that makes "report by hash, never omit" true at
    /// the point a caller prints a row.
    /// </remarks>
    public string Display => Name ?? Hash.ToString(CultureInfo.InvariantCulture);
}
