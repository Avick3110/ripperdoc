namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// One log, and the instant its own contents put it at.
/// </summary>
/// <param name="FileName">The log's file name, without its directory.</param>
/// <param name="Instant">
/// The first instant the log's head yielded, or null where it yielded none.
/// </param>
/// <param name="Grammar">
/// The grammar that read <paramref name="Instant" />, or null where nothing
/// did.
/// </param>
/// <remarks>
/// <para>
/// <strong>The file name is carried for the reader, never used to attribute.</strong>
/// One framework names a rotated log after the boot that displaced it while
/// filling it with the previous boot's content, so a name and an instant
/// disagreeing is a measured condition rather than a defect - and the instant
/// is the one that is right.
/// </para>
/// <para>
/// A null <paramref name="Instant" /> is the honest outcome for a log this
/// reader's declared grammars cannot read, and it is reported rather than
/// resolved: attributing such a file by its name would be correct for the
/// families that stamp honestly and wrong for the one that rotates, and
/// nothing in the file itself says which family it belongs to.
/// </para>
/// </remarks>
public sealed record AttributedLog(string FileName, DateTime? Instant, string? Grammar)
{
    /// <summary>Whether this log's own contents placed it at an instant.</summary>
    public bool IsAttributed => Instant is not null;
}
