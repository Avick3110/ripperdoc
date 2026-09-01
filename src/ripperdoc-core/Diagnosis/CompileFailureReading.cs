using System.Globalization;
using System.Text.RegularExpressions;

namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// What one compiler log says went wrong, and which mods the deployment record
/// attributes it to.
/// </summary>
/// <remarks>
/// <para>
/// The attribution is a composition and not a judgement: an error names a
/// source file, the deployment record says which mod supplied that file, and
/// the mod is what the record says. Nothing here decides which mod is at fault
/// among several, because nothing measured says how to.
/// </para>
/// <para>
/// The boot this reading belongs to comes from
/// <see cref="LogAttribution" /> - the log's own contents - and never from the
/// file name. A compiler log named after the boot that displaced it is the
/// measured case, and attributing these errors by the name would blame this
/// boot's mods for the previous boot's failure.
/// </para>
/// </remarks>
public sealed partial class CompileFailureReading
{
    private CompileFailureReading(
        AttributedLog log,
        IReadOnlyList<CompileError> errors,
        IReadOnlyList<CompileSuspect> suspects,
        IReadOnlyList<string> sourcesOutsideTheGameDirectory,
        IReadOnlyList<string> sourcesTheRecordDoesNotClaim,
        int errorLinesNotRead)
    {
        Log = log;
        Errors = errors;
        Suspects = suspects;
        SourcesOutsideTheGameDirectory = sourcesOutsideTheGameDirectory;
        SourcesTheRecordDoesNotClaim = sourcesTheRecordDoesNotClaim;
        ErrorLinesNotRead = errorLinesNotRead;
    }

    /// <summary>The log, and the boot its own contents place it at.</summary>
    public AttributedLog Log { get; }

    /// <summary>Every error line this reader understood.</summary>
    public IReadOnlyList<CompileError> Errors { get; }

    /// <summary>The mods implicated, each with the errors that implicate it.</summary>
    public IReadOnlyList<CompileSuspect> Suspects { get; }

    /// <summary>
    /// Sources the compiler named from outside the game directory the record
    /// covers.
    /// </summary>
    /// <remarks>
    /// Reported rather than dropped. A source the record could never have
    /// claimed is a different fact from one it should have claimed and did not,
    /// and folding the two together would hide whichever is rarer.
    /// </remarks>
    public IReadOnlyList<string> SourcesOutsideTheGameDirectory { get; }

    /// <summary>Sources inside the game directory that the record attributes to nothing.</summary>
    public IReadOnlyList<string> SourcesTheRecordDoesNotClaim { get; }

    /// <summary>
    /// Error lines this reader did not understand.
    /// </summary>
    /// <remarks>
    /// A count rather than silence. A compiler run ends with a summary line
    /// that is an error line carrying no source, so a non-zero count is
    /// ordinary - what it must not be is invisible, because a reader with no
    /// count cannot tell a summary line from a shape this engine has never
    /// seen.
    /// </remarks>
    public int ErrorLinesNotRead { get; }

    /// <summary>
    /// Reads a compiler log and attributes its errors through a deployment
    /// record.
    /// </summary>
    /// <param name="logPath">The compiler log.</param>
    /// <param name="record">The deployment record covering the game directory.</param>
    /// <param name="gameDirectory">The directory the record's paths are relative to.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="IOException">The log could not be read.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="gameDirectory" /> is empty or only whitespace.
    /// </exception>
    /// <exception cref="DiagnosisReadException">
    /// The record attributes one deployed path to more than one mod.
    /// </exception>
    public static CompileFailureReading Of(string logPath, DeploymentRecord record, string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(logPath);

        var (log, text) = LogAttribution.PlacedWithText(logPath);
        return Read(log, text, record, gameDirectory);
    }

    /// <summary>
    /// Reads a compiler log whose text is already in hand.
    /// </summary>
    /// <param name="log">The log, already placed at a boot.</param>
    /// <param name="text">Its whole text.</param>
    /// <param name="record">The deployment record covering the game directory.</param>
    /// <param name="gameDirectory">The directory the record's paths are relative to.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="gameDirectory" /> is empty or only whitespace.
    /// </exception>
    /// <exception cref="DiagnosisReadException">
    /// The record attributes one deployed path to more than one mod.
    /// </exception>
    public static CompileFailureReading Read(
        AttributedLog log,
        string text,
        DeploymentRecord record,
        string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(gameDirectory);

        // A record whose target path this reader could not find parses with an
        // empty one, and an empty one is a prefix every path fails: every error
        // would be filed as outside the game directory and no mod named at all.
        // That is a reading that says nothing while looking like one that found
        // nothing, so it is refused where it would be produced.
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            throw new ArgumentException(
                "The directory the record's paths are relative to is empty, so no source the "
                + "compiler names could be resolved against it and every error would be reported "
                + "as outside the game directory. A record carrying no target path cannot "
                + "attribute a compile failure.",
                nameof(gameDirectory));
        }

        var errors = new List<CompileError>();
        var unread = 0;

        foreach (var line in text.Split('\n'))
        {
            var match = ErrorLine().Match(line);

            // The digit groups are bounded by nothing, and the pattern's \d
            // reaches every Unicode decimal rather than the ASCII ones, so a
            // position too large for the type or written in another script
            // reaches this. Constructing from one throws out of a read, which
            // ends a diagnosis rather than reporting it - so a position that
            // cannot be constructed makes the line one this reader did not
            // understand, which is a count it already carries.
            if (match.Success
                && Position(match, "line") is { } lineNumber
                && Position(match, "column") is { } columnNumber)
            {
                errors.Add(new CompileError(
                    match.Groups["code"].Value,
                    match.Groups["path"].Value,
                    lineNumber,
                    columnNumber));
            }
            else if (AnyErrorLine().IsMatch(line))
            {
                unread++;
            }
        }

        var claims = record.ClaimsByPath();

        var prefix = DeploymentRecord.Normalised(gameDirectory).TrimEnd('/') + "/";
        var byMod = new SortedDictionary<string, List<CompileError>>(StringComparer.Ordinal);
        var outside = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unclaimed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in errors)
        {
            var path = DeploymentRecord.Normalised(error.SourcePath);

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                outside.Add(error.SourcePath);
                continue;
            }

            if (!claims.TryGetValue(path[prefix.Length..], out var mod))
            {
                unclaimed.Add(error.SourcePath);
                continue;
            }

            if (!byMod.TryGetValue(mod, out var theirs))
            {
                theirs = [];
                byMod[mod] = theirs;
            }

            theirs.Add(error);
        }

        return new CompileFailureReading(
            log,
            errors,
            [.. byMod.Select(pair => new CompileSuspect(pair.Key, pair.Value))],
            [.. outside],
            [.. unclaimed],
            unread);
    }

    private static int? Position(Match match, string group) =>
        int.TryParse(
            match.Groups[group].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    // The level is matched rather than captured and dropped. A diagnostic the
    // compiler did not raise as an error carries the same shape after its
    // level, so a reader ignoring the level turns a warning into a suspect -
    // and a suspect is an accusation against a named mod.
    [GeneratedRegex(
        @"^\[ERROR - [^\]]*\] \[(?<code>\w+)\] At (?<path>.+?):(?<line>\d+):(?<column>\d+):",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ErrorLine();

    // Case-folded alongside it, so the two cannot disagree about what an error
    // line is. A level spelled differently would otherwise be read by neither -
    // neither understood nor counted as not understood, which is the one
    // outcome the count exists to prevent.
    [GeneratedRegex(@"^\[ERROR", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AnyErrorLine();
}
