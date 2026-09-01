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

        var errors = new List<CompileError>();
        var unread = 0;

        foreach (var line in text.Split('\n'))
        {
            var match = ErrorLine().Match(line);

            if (match.Success)
            {
                errors.Add(new CompileError(
                    match.Groups["code"].Value,
                    match.Groups["path"].Value,
                    int.Parse(match.Groups["line"].Value),
                    int.Parse(match.Groups["column"].Value)));
            }
            else if (AnyErrorLine().IsMatch(line))
            {
                unread++;
            }
        }

        var claims = record.Files.ToDictionary(
            file => Normalised(file.RelativePath), file => file.SourceMod, StringComparer.OrdinalIgnoreCase);

        var prefix = Normalised(gameDirectory).TrimEnd('/') + "/";
        var byMod = new SortedDictionary<string, List<CompileError>>(StringComparer.Ordinal);
        var outside = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unclaimed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in errors)
        {
            var path = Normalised(error.SourcePath);

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

    private static string Normalised(string path) => path.Replace('\\', '/');

    [GeneratedRegex(
        @"^\[(?<level>\w+) - [^\]]*\] \[(?<code>\w+)\] At (?<path>.+?):(?<line>\d+):(?<column>\d+):",
        RegexOptions.CultureInvariant)]
    private static partial Regex ErrorLine();

    [GeneratedRegex(@"^\[ERROR", RegexOptions.CultureInvariant)]
    private static partial Regex AnyErrorLine();
}
