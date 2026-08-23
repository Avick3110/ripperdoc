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
/// checks the receipt against the one input it does have - the pinned
/// dependency's own file, which is on disk wherever this engine builds. So a
/// build can say, and says only, this: the accepted result of the audit was
/// taken against exactly this build of the dependency. Bump the dependency
/// without re-running the audit and that stops being true, loudly.
/// </para>
/// <para>
/// The claim is deliberately about identity and not about content. What a
/// machine with no generated information can establish for itself is which
/// build of the dependency it holds, which the file states and which cannot
/// move while the file does not. What that build's type model actually says is
/// read by reflecting over it, and a reflected reading is not stable enough to
/// key a committed file on - the model's answer for a small number of
/// properties depends on what else in it has been resolved first. So identity
/// is what travels here, and content is compared where the generated
/// information is, by the audit, which holds the two descriptions against each
/// other outright.
/// </para>
/// <para>
/// The reading is nonetheless written down, and used only where the comparison
/// happens. A machine with no generated information cannot check it and does
/// not try; a machine that can compare uses it to tell "the two descriptions
/// disagree" from "this process read the model differently from the one the
/// accepted result came out of", and announces the second as a comparison not
/// run. See <see cref="TypeModelReadingFingerprint"/> and
/// <see cref="ReflectionReadingPin"/>; this is interim.
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
    /// What identifies the build of the pinned dependency the audit was run
    /// against.
    /// </summary>
    /// <remarks>
    /// The one field a machine with no generated type information can check for
    /// itself, and therefore the whole of what makes this receipt more than an
    /// assertion. It says which build of the dependency was audited and not
    /// what that build contains - see the type's own remarks for why the second
    /// is not something a committed file can carry.
    /// </remarks>
    [JsonPropertyName("typeModelAssemblyIdentity")]
    public required string TypeModelAssemblyIdentity { get; init; }

    /// <summary>
    /// A fingerprint of the reading of the pinned model that the accepted
    /// result was actually taken from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reflecting the compiled model does not give the same answer twice. The
    /// answer is settled once per process and holds for that process's life, so
    /// reading it again changes nothing - and across processes a small number
    /// of properties take a foreign, unrelated stored type. Roughly a quarter
    /// of processes measured on one machine carried exactly one such property,
    /// and it was a different property each time.
    /// </para>
    /// <para>
    /// So this is not a second identity for the dependency;
    /// <see cref="TypeModelAssemblyIdentity"/> is that, and it is stable. This
    /// says which of the readings that one build can produce the accepted
    /// divergences were compared against. A process that reads differently is
    /// holding the game's description against a different opposite number, and
    /// what it finds says nothing about drift - so the comparison is announced
    /// as not run rather than run and failed.
    /// </para>
    /// <para>
    /// Interim. See <see cref="ReflectionReadingPin"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("typeModelReadingFingerprint")]
    public required string TypeModelReadingFingerprint { get; init; }

    /// <summary>
    /// What standing the reading pin has, in the file rather than only in the
    /// code that writes it.
    /// </summary>
    /// <remarks>
    /// Pinning a reading works around an instability rather than fixing it, and
    /// whoever reads this file in a year should not have to infer that from the
    /// code. The instability is filed, and until it is answered a run that draws
    /// a different reading skips the comparison instead of reporting drift
    /// nobody caused.
    /// </remarks>
    [JsonPropertyName("reflectionReadingPin")]
    public required string ReflectionReadingPin { get; init; }

    /// <summary>
    /// The standing every receipt this build writes records for the reading
    /// pin, and the question that would retire it.
    /// </summary>
    public const string InterimPinStatus = "interim pending issue #22";

    /// <summary>
    /// What the generated type information was, in the words its own provenance
    /// uses - the game version it came from, and nothing of what it contains.
    /// </summary>
    [JsonPropertyName("generatedFrom")]
    public required string GeneratedFrom { get; init; }

    /// <summary>
    /// A fingerprint of everything the generated type information said, in the
    /// vocabulary the audit compares in.
    /// </summary>
    /// <remarks>
    /// The other side's identity, and unlike the compiled side's it is taken
    /// from content: generated type information is read out of files that say
    /// the same thing every time, so a fingerprint of the reading is stable and
    /// is the more exact statement of which input was audited. It is a hash and
    /// names nothing the game declares. A check that reproduces a number
    /// measured against one dump can require this to match before believing the
    /// number applies.
    /// </remarks>
    [JsonPropertyName("generatedTypeInformationFingerprint")]
    public required string GeneratedTypeInformationFingerprint { get; init; }

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
    /// <param name="generated">The generated type information it was run against.</param>
    /// <returns>The receipt.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <remarks>
    /// This is what regenerates the committed receipt. It is called where the
    /// generated type information is - the audit's own tier - and what it
    /// produces is written out beside the checks that ran it, so accepting a
    /// new result is copying a file rather than editing one by hand.
    /// </remarks>
    public static DriftReceipt Of(
        TypeModelAudit audit,
        CompiledTypeModelReading compiled,
        TypeModelReading generated)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(generated);

        return new DriftReceipt
        {
            Dependency = compiled.DependencyVersion,
            TypeModelAssemblyIdentity = compiled.AssemblyIdentity,
            TypeModelReadingFingerprint = compiled.Reading.Fingerprint(),
            ReflectionReadingPin = InterimPinStatus,
            GeneratedFrom = audit.GeneratedDescription,
            GeneratedTypeInformationFingerprint = generated.Fingerprint(),
            ClassesCompared = audit.ClassesCompared,
            PropertiesCompared = audit.PropertiesCompared,
            EnumsCompared = audit.EnumsCompared,
            EnumMembersCompared = audit.EnumMembersCompared,
            DivergenceCounts = audit.CountsByKind()
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal),
            DivergenceFingerprint = audit.DivergenceFingerprint,
            ReadFailures = compiled.Reading.Failures.Count + generated.Failures.Count,
        };
    }

    /// <summary>The name the receipt is kept under, wherever it is kept.</summary>
    /// <remarks>
    /// One home for the name, so that the committed file, the check that holds
    /// a build against it, and the message telling a reader which file to
    /// replace cannot come to disagree about what it is called.
    /// </remarks>
    public const string FileName = "drift-receipt.json";

    /// <summary>
    /// The name a freshly taken receipt is written out under, beside the
    /// committed one.
    /// </summary>
    /// <remarks>
    /// Deliberately not the committed name. A run that overwrote the committed
    /// receipt would turn every red drift check green by the act of running,
    /// which is the one failure mode a gate like this cannot have.
    /// </remarks>
    public const string ProducedFileName = "drift-receipt.produced.json";

    /// <summary>
    /// The name of the file a tier (iii) run leaves behind saying whether the
    /// comparison actually happened.
    /// </summary>
    /// <remarks>
    /// Whether the comparison can run is decided inside the process that would
    /// run it, because what decides it is how that process reflected the model -
    /// a property no gate script can read from outside, and one that a separate
    /// probe process would answer for itself rather than for the run. So the run
    /// says which it did, here, and the gate reads it and reports a skip by
    /// name. Written on every run, matched or not: a gate that only looked for
    /// this file when something was wrong could not tell a run that compared
    /// from a run whose report never arrived.
    /// </remarks>
    public const string AuditStatusFileName = "drift-audit-status.txt";

    /// <summary>What that file says when the comparison did happen.</summary>
    public const string AuditRanStatus = "ran";

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
