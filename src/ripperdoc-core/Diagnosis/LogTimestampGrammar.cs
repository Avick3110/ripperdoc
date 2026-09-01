using System.Globalization;
using System.Text.RegularExpressions;
using Ripperdoc.Core.Reporting;

namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// A shape a timestamp takes inside a log, and a line that shape must read.
/// </summary>
/// <remarks>
/// <para>
/// The set is declared here and read back from these declarations, so a
/// grammar cannot be added in one place and forgotten in another. Each member
/// carries a <see cref="Witness" /> it is required to parse, which is what
/// makes the completeness check able to fail: a member whose own witness it
/// cannot read is a member that would silently never match a real log.
/// </para>
/// <para>
/// <strong>The set is this reader's, not the frameworks'.</strong> Nothing
/// inside this project can enumerate what a framework might write, so a log
/// whose head yields none of these is reported unattributable rather than
/// attributed by its file name — see
/// <c>findings/2026-09-01-log-attribution.md</c> for why the file name is
/// never the fallback.
/// </para>
/// </remarks>
public sealed class LogTimestampGrammar : IWitnessedGrammar
{
    /// <summary>A bracketed date and time, as the plugin loader and its plugins write.</summary>
    public static readonly LogTimestampGrammar Bracketed = new(
        @"\[(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2}) (?<h>\d{2}):(?<mi>\d{2}):(?<s>\d{2})",
        "[2026-01-02 03:04:05.678] [1234] [info] a plugin is starting...");

    /// <summary>A bracketed level and long-form date, as the script compiler writes.</summary>
    public static readonly LogTimestampGrammar LongForm = new(
        @"\[\w+ - \w{3}, (?<d>\d{2}) (?<mon>\w{3}) (?<y>\d{4}) (?<h>\d{2}):(?<mi>\d{2}):(?<s>\d{2})",
        "[INFO - Thu, 02 Jan 2026 03:04:05 +0100] Compiling files in <scripts>:");

    /// <summary>An unbracketed date and time opening a line, as a tabular log writes.</summary>
    /// <remarks>
    /// Its first line is a column header, so the timestamp this grammar reads
    /// is never on the line a first-line rule would look at. That file is the
    /// reason the rule is "the first stamp the head yields" rather than "the
    /// stamp on the first line".
    /// </remarks>
    public static readonly LogTimestampGrammar LineLeading = new(
        @"^(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})T(?<h>\d{2}):(?<mi>\d{2}):(?<s>\d{2})",
        "timestamp\tlevel\tevent\n2026-01-02T03:04:05.678\tINFO\tstarted\n");

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private readonly Regex _pattern;

    private LogTimestampGrammar(string pattern, string witness)
    {
        _pattern = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        Witness = witness;
        DeclaredKinds.Register(this);
    }

    /// <summary>A line this grammar is required to read.</summary>
    public string Witness { get; }

    /// <summary>
    /// The first instant this grammar finds in <paramref name="head" />, and
    /// where it found it.
    /// </summary>
    /// <param name="head">The opening of a log.</param>
    /// <returns>
    /// The offset the match began at and the instant read, or null if this
    /// grammar finds nothing.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="head" /> is null.</exception>
    /// <remarks>
    /// No zone is read even where the log carries one. Two logs from one boot
    /// are compared against each other rather than against a clock, and a zone
    /// parsed from one framework and absent from another would make the
    /// comparison depend on which frameworks happened to be installed.
    /// </remarks>
    public (int Offset, DateTime Instant)? FirstIn(string head)
    {
        ArgumentNullException.ThrowIfNull(head);

        for (var match = _pattern.Match(head); match.Success; match = match.NextMatch())
        {
            if (Instant(match) is { } instant)
            {
                return (match.Index, instant);
            }
        }

        return null;
    }

    /// <remarks>
    /// A stamp whose fields are out of range is skipped rather than ending the
    /// search. A log opening with one would otherwise be unattributable on
    /// account of a line that is not the line carrying its boot.
    /// </remarks>
    private static DateTime? Instant(Match match)
    {
        var month = match.Groups["mo"].Success
            ? Number(match, "mo")
            : Array.IndexOf(Months, match.Groups["mon"].Value) + 1;

        if (month is < 1 or > 12)
        {
            return null;
        }

        var day = Number(match, "d");
        var year = Number(match, "y");

        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        var hour = Number(match, "h");
        var minute = Number(match, "mi");
        var second = Number(match, "s");

        // Every field is two digits by the pattern and nothing narrower, so a
        // log carrying a malformed stamp reaches here. Constructing from one
        // would throw out of a read.
        if (hour > 23 || minute > 59 || second > 59)
        {
            return null;
        }

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
    }

    private static int Number(Match match, string group) =>
        int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
}
