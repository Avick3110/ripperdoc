using System.Text;
using Ripperdoc.Core.Reporting;

namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// Places logs at the instant their own contents carry.
/// </summary>
/// <remarks>
/// <para>
/// Every declared grammar is tried and the match at the lowest offset wins, so
/// the instant returned is the first one the head yields under any of them
/// rather than the first one a preferred grammar finds. Preferring a grammar
/// would make the answer depend on the order the set happens to be declared
/// in.
/// </para>
/// <para>
/// Only the opening of a file is read. A log whose first stamp lies beyond
/// <see cref="HeadBytes" /> is reported unattributed, which is a bounded and
/// visible outcome; reading whole logs to find one would make attribution cost
/// scale with a corpus that runs to megabytes per boot.
/// </para>
/// </remarks>
public static class LogAttribution
{
    /// <summary>How much of a log's opening is read.</summary>
    public const int HeadBytes = 64 * 1024;

    /// <summary>
    /// Places one log by its contents.
    /// </summary>
    /// <param name="path">The log to read.</param>
    /// <returns>The log, with the instant its head yielded or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static AttributedLog Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Read(Path.GetFileName(path), Head(path));
    }

    /// <summary>
    /// Places a log whose contents are already in hand.
    /// </summary>
    /// <param name="fileName">The log's file name.</param>
    /// <param name="head">Its opening.</param>
    /// <returns>The log, with the instant its head yielded or none.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static AttributedLog Read(string fileName, string head) =>
        ReadWith<LogTimestampGrammar>(fileName, head);

    /// <summary>
    /// Places a log under a named grammar set.
    /// </summary>
    /// <typeparam name="TGrammar">The grammar set to read under.</typeparam>
    /// <param name="fileName">The log's file name.</param>
    /// <param name="head">Its opening.</param>
    /// <remarks>
    /// Generic so the completeness check runs this entry point rather than a
    /// copy of it. A grammar asked directly whether it reads the witness built
    /// for it is being compared with itself.
    /// </remarks>
    internal static AttributedLog ReadWith<TGrammar>(string fileName, string head)
        where TGrammar : class, IWitnessedGrammar
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(head);

        var earliest = default((int Offset, DateTime Instant)?);
        string? grammar = null;

        foreach (var declared in DeclaredKinds.Of<TGrammar>())
        {
            var found = declared.Kind.FirstIn(head);
            if (found is null || (earliest is not null && found.Value.Offset >= earliest.Value.Offset))
            {
                continue;
            }

            earliest = found;
            grammar = declared.Name;
        }

        return new AttributedLog(fileName, earliest?.Instant, grammar);
    }

    private static string Head(string path)
    {
        using var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var buffer = new byte[HeadBytes];
        var filled = 0;

        while (filled < buffer.Length)
        {
            var read = file.Read(buffer, filled, buffer.Length - filled);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        // A log a running game holds open is the ordinary case rather than the
        // exception, which is why the share is permissive and the read is
        // bounded by what the file gives rather than by its reported length.
        return Encoding.UTF8.GetString(buffer, 0, filled);
    }
}
