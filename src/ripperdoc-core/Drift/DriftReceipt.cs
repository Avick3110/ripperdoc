using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripperdoc.Core.Drift;

/// <summary>
/// A record that the drift audit ran, what it was run against, and what it
/// found - small enough and impersonal enough to be committed.
/// </summary>
/// <remarks>
/// <para>
/// The audit needs type information generated from a game install, and a
/// machine that builds this engine will usually not have one. That leaves a gap
/// the obvious designs both fall into: an audit that only ever runs where a
/// dump is means the build says nothing about drift, and an audit that runs
/// everywhere means it fails on every machine that is perfectly healthy.
/// </para>
/// <para>
/// This is the third answer. The audit runs where the generated information is;
/// what it produces is this receipt; and a build with no generated information
/// checks the receipt against the one input it does have - the compiled type
/// model, which is on disk wherever this engine builds. So a build can say, and
/// says only, this: the accepted result of the audit was taken against exactly
/// the type model in this build. Bump the dependency without re-running the
/// audit and that stops being true, loudly.
/// </para>
/// <para>
/// What it deliberately cannot say is whether the audit is current with respect
/// to a newer game. Nothing without a dump can know that, so the receipt names
/// the game it was taken against and leaves the question to a machine that can
/// answer it, rather than answering it wrongly.
/// </para>
/// <para>
/// It carries counts, fingerprints and versions, and no type or member the game
/// declares. What diverged is derived from the game's own type information and
/// belongs on the machine that generated it; that it diverged, and whether that
/// has changed, is what a shared record needs.
/// </para>
/// </remarks>
public sealed record DriftReceipt
{
    /// <summary>The pinned dependency the audit was run against.</summary>
    [JsonPropertyName("dependency")]
    public required string Dependency { get; init; }

    /// <summary>
    /// A fingerprint of the compiled type model the audit was run against.
    /// </summary>
    /// <remarks>
    /// The one field a machine with no generated type information can check for
    /// itself, and therefore the whole of what makes this receipt more than an
    /// assertion.
    /// </remarks>
    [JsonPropertyName("compiledTypeModelFingerprint")]
    public required string CompiledTypeModelFingerprint { get; init; }

    /// <summary>
    /// What the generated type information was, in the words its own provenance
    /// uses - the game version it came from, and nothing of what it contains.
    /// </summary>
    [JsonPropertyName("generatedFrom")]
    public required string GeneratedFrom { get; init; }

    /// <summary>How many classes were compared.</summary>
    [JsonPropertyName("classesCompared")]
    public required int ClassesCompared { get; init; }

    /// <summary>How many properties were compared.</summary>
    [JsonPropertyName("propertiesCompared")]
    public required int PropertiesCompared { get; init; }

    /// <summary>How many enumerations were compared.</summary>
    [JsonPropertyName("enumsCompared")]
    public required int EnumsCompared { get; init; }

    /// <summary>How many enumeration members were compared.</summary>
    [JsonPropertyName("enumMembersCompared")]
    public required int EnumMembersCompared { get; init; }

    /// <summary>How many divergences of each kind the audit found.</summary>
    [JsonPropertyName("divergenceCounts")]
    public required IReadOnlyDictionary<string, int> DivergenceCounts { get; init; }

    /// <summary>
    /// A fingerprint of exactly the set of divergences that was accepted.
    /// </summary>
    /// <remarks>
    /// The audit is not expected to find nothing. It finds a handful of real
    /// disagreements today, and a gate that went red for those would be turned
    /// off within a week. What is worth failing on is the set <em>changing</em>,
    /// which a count cannot see and this can.
    /// </remarks>
    [JsonPropertyName("divergenceFingerprint")]
    public required string DivergenceFingerprint { get; init; }

    /// <summary>
    /// What could not be read on either side while the audit ran.
    /// </summary>
    /// <remarks>
    /// Counted rather than described, and present even at zero. A type neither
    /// description could be read for is one the audit cannot have found drift
    /// in, and a receipt that mentioned it only when it happened would read as
    /// a clean audit on every run that never checked.
    /// </remarks>
    [JsonPropertyName("readFailures")]
    public required int ReadFailures { get; init; }

    /// <summary>
    /// Take a receipt from an audit that has just run.
    /// </summary>
    /// <param name="audit">The audit.</param>
    /// <param name="compiled">The compiled model it was run against.</param>
    /// <param name="generatedFailures">
    /// How many things the generated side could not be read for.
    /// </param>
    /// <returns>The receipt.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static DriftReceipt Of(TypeModelAudit audit, CompiledTypeModelReading compiled, int generatedFailures)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(compiled);

        return new DriftReceipt
        {
            Dependency = compiled.DependencyVersion,
            CompiledTypeModelFingerprint = compiled.Reading.Fingerprint(),
            GeneratedFrom = audit.GeneratedDescription,
            ClassesCompared = audit.ClassesCompared,
            PropertiesCompared = audit.PropertiesCompared,
            EnumsCompared = audit.EnumsCompared,
            EnumMembersCompared = audit.EnumMembersCompared,
            DivergenceCounts = audit.CountsByKind()
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal),
            DivergenceFingerprint = audit.DivergenceFingerprint,
            ReadFailures = compiled.Reading.Failures.Count + generatedFailures,
        };
    }

    /// <summary>Read a receipt from the file it is kept in.</summary>
    /// <param name="path">The receipt's path.</param>
    /// <returns>The receipt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">There is no receipt there.</exception>
    /// <exception cref="JsonException">The file is not a readable receipt.</exception>
    public static DriftReceipt Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "There is no drift receipt at this path, so nothing records that the audit ever ran.",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<DriftReceipt>(stream, Format)
            ?? throw new JsonException($"'{Path.GetFileName(path)}' holds no receipt.");
    }

    /// <summary>The receipt as it is written to its file.</summary>
    /// <returns>The receipt's text, ending in a newline.</returns>
    /// <remarks>
    /// Line endings are the same on every machine. This file is committed and
    /// compared against what a later run produces, so a receipt written on one
    /// operating system and checked on another would otherwise differ without
    /// anything about the audit having changed.
    /// </remarks>
    public string ToJson() => JsonSerializer.Serialize(this, Format).ReplaceLineEndings("\n") + "\n";

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
    };
}
