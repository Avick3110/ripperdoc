using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// The schema artifact in the form it is kept in between runs.
/// </summary>
/// <remarks>
/// <para>
/// Generating the schema from type information means reading tens of thousands
/// of files, and there is no reason to do that more than once for one install.
/// This is what the generation step leaves behind and what every later run
/// picks up instead.
/// </para>
/// <para>
/// What is persisted is what the source <em>said</em> - the type shapes as they
/// were read, before inheritance is resolved - and not the resolved schema.
/// Reading it back runs the same derivation over the same shapes, so a
/// reloaded schema is the derivation's output rather than a copy of it that
/// could drift from what the derivation would now produce. A resolved schema
/// written out would also be several times the size, since a field declared
/// once and inherited by fifty types is one shape and fifty-one slots.
/// </para>
/// <para>
/// The provenance block is not persisted either, for the same reason: it is
/// computed from the schema and the manifest, so writing it down would be a
/// second home for a fact that can go stale against the first. What a reader
/// gets back is recomputed, and therefore true of what was actually loaded.
/// </para>
/// <para>
/// This file is derived from a game install and belongs on the machine that
/// generated it. Nothing here goes into this project's repository.
/// </para>
/// </remarks>
public sealed record SchemaIrDocument
{
    /// <summary>
    /// Which version of this format the file is written in.
    /// </summary>
    /// <remarks>
    /// Read before anything else and refused when it is not one this build
    /// knows. A format that changed shape without saying so would be read as
    /// though it had not, and the wrong answers would come out of the schema
    /// rather than out of the reader.
    /// </remarks>
    [JsonPropertyName("formatVersion")]
    public required int FormatVersion { get; init; }

    /// <summary>Which mode produced the schema.</summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    /// <summary>What the type information was read from.</summary>
    [JsonPropertyName("typeInformationSource")]
    public required string TypeInformationSource { get; init; }

    /// <summary>When the artifact was generated.</summary>
    [JsonPropertyName("generatedAt")]
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Which build of the game was installed when this artifact was generated,
    /// or null where no install was named or none could be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Persisted rather than recomputed, unlike everything else here, because
    /// it is the one fact about this artifact that cannot be recovered from
    /// the artifact. It is what makes staleness checkable: a later run compares
    /// it against the build now installed and can say the game has moved since,
    /// instead of assuming either way.
    /// </para>
    /// <para>
    /// It is deliberately not "the build the type information described", which
    /// is the question anyone reading it actually has. Nothing in the type
    /// information states a build, so that question cannot be answered from
    /// here at all; the two coincide when the type information was produced
    /// from the install it is being generated against, and part company when it
    /// was produced before a patch. Widening this field's meaning to the
    /// question would make it wrong in exactly that case rather than silent,
    /// and a run that generated from older type information would report an
    /// artifact current with a game it does not describe.
    /// </para>
    /// </remarks>
    [JsonPropertyName("gameBuild")]
    public string? GameBuild { get; init; }

    /// <summary>The type shapes, as the source reported them.</summary>
    [JsonPropertyName("types")]
    public required IReadOnlyList<PersistedType> Types { get; init; }

    /// <summary>What the source could not derive.</summary>
    [JsonPropertyName("derivationFailures")]
    public required IReadOnlyList<PersistedFailure> DerivationFailures { get; init; }

    /// <summary>
    /// What shipped data confirmed, or null where nothing arbitrated the
    /// schema.
    /// </summary>
    [JsonPropertyName("validation")]
    public PersistedValidation? Validation { get; init; }

    /// <summary>The version of this format this build writes and reads.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// Put an artifact into the form it is kept in.
    /// </summary>
    /// <param name="artifact">The artifact.</param>
    /// <param name="reading">
    /// The reading the artifact's schema was derived from. Persisting the
    /// shapes rather than the resolved schema is what lets a later run re-run
    /// the derivation instead of trusting a copy of its output.
    /// </param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <param name="gameBuild">
    /// Which build of the game the type information described, or null where
    /// nothing recorded it.
    /// </param>
    public static SchemaIrDocument Of(
        SchemaIr artifact,
        RecordTypeSourceReading reading,
        string? gameBuild = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(reading);

        // Where an arbiter was present, the candidate spellings have already
        // served their purpose and are not written down again. A schema that
        // was checked against real values knows which name those values are
        // keyed by, and carrying the guesses forward would leave every later
        // run probing under names the data has already ruled out - and would
        // leave the artifact unable to say whether a spelling in it is a fact
        // or a guess. Where no arbiter was present, or where it found nothing
        // to decide with, the candidates stay and the provenance says so.
        var resolved = artifact.Validation is null ? null : ConfirmedSpellings(artifact.Validation);

        return new SchemaIrDocument
        {
            FormatVersion = CurrentFormatVersion,
            Mode = artifact.Provenance.Mode.ToString(),
            TypeInformationSource = artifact.Provenance.TypeInformationSource,
            GeneratedAt = artifact.Provenance.GeneratedAt,
            GameBuild = gameBuild,
            Types = reading.Types
                .OrderBy(type => type.TypeName, StringComparer.Ordinal)
                .Select(type => new PersistedType
                {
                    Name = type.TypeName,
                    BaseTypeName = type.BaseTypeName,
                    IsRecordType = type.IsRecordType,
                    Fields = type.DeclaredFields
                        .OrderBy(field => field.FieldName, StringComparer.Ordinal)
                        .Select(field => Persist(field, type.TypeName, resolved))
                        .ToArray(),
                })
                .ToArray(),
            DerivationFailures = reading.Failures
                .Select(failure => new PersistedFailure
                {
                    TypeName = failure.TypeName,
                    MemberName = failure.MemberName,
                    Reason = failure.Reason,
                })
                .ToArray(),
            Validation = artifact.Validation is null
                ? null
                : PersistedValidation.Of(artifact.Validation, resolved),
        };
    }

    /// <summary>
    /// Which names the data vindicated, for each field a source declared.
    /// </summary>
    /// <remarks>
    /// Keyed by the type that declares the field rather than by the types that
    /// carry it. One declaration is inherited by many record types and is
    /// written down once, so the evidence from every type that carries it is
    /// evidence about the one declaration - and a name confirmed on any of them
    /// is confirmed.
    /// </remarks>
    private static Dictionary<(string Type, string Field), IReadOnlyList<string>> ConfirmedSpellings(
        ValidationManifest manifest)
    {
        var byDeclaration = new Dictionary<(string, string), SortedSet<string>>();

        foreach (var verdict in manifest.Fields())
        {
            foreach (var confirmed in verdict.ConfirmedFieldNames)
            {
                var key = (verdict.DeclaringTypeName, verdict.FieldName);
                if (!byDeclaration.TryGetValue(key, out var names))
                {
                    byDeclaration[key] = names = new SortedSet<string>(StringComparer.Ordinal);
                }

                names.Add(confirmed);
            }
        }

        return byDeclaration.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.ToArray());
    }

    /// <summary>
    /// The name a field is written down under, and which other names go with it.
    /// </summary>
    /// <remarks>
    /// The schema's own spelling is kept where the data confirmed it. Where the
    /// data confirmed only another candidate, that one becomes the name -
    /// which is the whole return on arbitrating: the guess the source led with
    /// was wrong, real values say so, and a later run should not have to
    /// rediscover it.
    /// </remarks>
    private static PersistedField Persist(
        RecordFieldShape field,
        string declaringTypeName,
        IReadOnlyDictionary<(string Type, string Field), IReadOnlyList<string>>? resolved)
    {
        var confirmed = resolved?.GetValueOrDefault((declaringTypeName, field.FieldName));

        if (confirmed is null || confirmed.Count == 0)
        {
            return new PersistedField
            {
                Name = field.FieldName,
                StorageType = field.StorageType,
                AlternateNames = field.AlternateFieldNames,
                ReferentTypeName = field.ReferentTypeName,
            };
        }

        var name = confirmed.Contains(field.FieldName, StringComparer.Ordinal)
            ? field.FieldName
            : confirmed[0];

        return new PersistedField
        {
            Name = name,
            StorageType = field.StorageType,
            AlternateNames = confirmed
                .Where(confirmedName => !string.Equals(confirmedName, name, StringComparison.Ordinal))
                .ToArray(),
            ReferentTypeName = field.ReferentTypeName,
        };
    }

    /// <summary>
    /// The name a field was written down under, for a caller that recorded a
    /// verdict against the name it had before arbitration.
    /// </summary>
    internal static string PersistedNameOf(
        string declaringTypeName,
        string fieldName,
        IReadOnlyDictionary<(string Type, string Field), IReadOnlyList<string>>? resolved)
    {
        var confirmed = resolved?.GetValueOrDefault((declaringTypeName, fieldName));

        return confirmed is null || confirmed.Count == 0
            || confirmed.Contains(fieldName, StringComparer.Ordinal)
                ? fieldName
                : confirmed[0];
    }

    /// <summary>
    /// The reading this document was written from.
    /// </summary>
    /// <returns>The type shapes and the failures.</returns>
    public RecordTypeSourceReading ToReading() => new(
        Types
            .Select(type => new RecordTypeShape(
                type.Name,
                type.BaseTypeName,
                type.IsRecordType,
                type.Fields
                    .Select(field => new RecordFieldShape(
                        field.Name,
                        field.StorageType,
                        field.AlternateNames,
                        field.ReferentTypeName))
                    .ToArray()))
            .ToArray(),
        DerivationFailures
            .Select(failure => new DerivationFailure(failure.TypeName, failure.MemberName, failure.Reason))
            .ToArray());

    /// <summary>
    /// Rebuild the artifact this document was written from.
    /// </summary>
    /// <returns>The artifact, its schema derived afresh from the persisted shapes.</returns>
    /// <exception cref="InvalidDataException">
    /// The document names a mode this build does not know.
    /// </exception>
    public SchemaIr ToArtifact()
    {
        if (!Enum.TryParse<SchemaMode>(Mode, out var mode))
        {
            throw new InvalidDataException(
                $"This artifact says it was produced in '{Mode}' mode, which this build does not know. What "
                + "a schema can be trusted to do depends on which mode made it, so it is refused rather than "
                + "read as though it came from a mode that happens to be familiar.");
        }

        var schema = RecordSchemaDerivation.Derive(ToReading(), TypeInformationSource);
        return SchemaIr.Create(schema, Validation?.ToManifest(), mode, GeneratedAt);
    }

    /// <summary>Read an artifact from the file it is kept in.</summary>
    /// <param name="path">The artifact's path.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">There is no artifact there.</exception>
    /// <exception cref="InvalidDataException">The file is not one this build can read.</exception>
    public static SchemaIrDocument Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("There is no schema artifact at this path.", path);
        }

        // The version is read on its own, and first. Read as part of the whole
        // document it cannot be: a later format that stops writing a key this
        // build requires fails to deserialise, and the version that would have
        // explained why is inside the document that did not parse. What the
        // reader then says is that the file is corrupt, of a file that is
        // perfectly well formed and simply newer - and whoever reads that goes
        // looking for damage instead of for a build that can read it.
        var version = VersionOf(path);

        if (version is null)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' does not say which artifact format it is written in, so there "
                + "is no way to tell a file this build can read from one it cannot.");
        }

        if (version != CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' is written in artifact format {version} and this build reads "
                + $"format {CurrentFormatVersion}. Generate the artifact again rather than reading it as "
                + "though the formats agreed.");
        }

        SchemaIrDocument? document;
        try
        {
            using var stream = File.OpenRead(path);
            document = JsonSerializer.Deserialize<SchemaIrDocument>(stream, Format);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' is not a readable schema artifact: {exception.Message}",
                exception);
        }

        if (document is null)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' holds no schema artifact.");
        }

        return document;
    }

    /// <summary>
    /// The format version a file states, or null where it states none.
    /// </summary>
    /// <remarks>
    /// Deserialised into a shape that requires nothing, so that it answers for
    /// a file written in any format this reader may ever meet. A file that is
    /// not JSON at all still fails here, which is the right answer for one: it
    /// is not a version this build cannot read, it is not an artifact.
    /// </remarks>
    private static int? VersionOf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<FormatVersionOnly>(stream, Format)?.FormatVersion;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' is not a readable schema artifact: {exception.Message}",
                exception);
        }
    }

    /// <summary>The one field that has to be readable before anything else is.</summary>
    private sealed record FormatVersionOnly
    {
        [JsonPropertyName("formatVersion")]
        public int? FormatVersion { get; init; }
    }

    /// <summary>Write the artifact to a file.</summary>
    /// <param name="path">Where to write it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public void Write(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, Format).ReplaceLineEndings("\n"));
    }

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>One type, as the source reported it.</summary>
public sealed record PersistedType
{
    /// <summary>The type's name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>What it inherits from, or null where the chain ends.</summary>
    [JsonPropertyName("base")]
    public string? BaseTypeName { get; init; }

    /// <summary>Whether it is itself a record type.</summary>
    [JsonPropertyName("isRecordType")]
    public required bool IsRecordType { get; init; }

    /// <summary>The fields it declares itself.</summary>
    [JsonPropertyName("fields")]
    public required IReadOnlyList<PersistedField> Fields { get; init; }
}

/// <summary>One declared field.</summary>
public sealed record PersistedField
{
    /// <summary>The field's name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The type its value is stored as.</summary>
    [JsonPropertyName("storageType")]
    public required string StorageType { get; init; }

    /// <summary>Other spellings its stored values might be keyed by.</summary>
    [JsonPropertyName("alternateNames")]
    public IReadOnlyList<string> AlternateNames { get; init; } = Array.Empty<string>();

    /// <summary>The kind of record it may point at, or null.</summary>
    [JsonPropertyName("referentType")]
    public string? ReferentTypeName { get; init; }
}

/// <summary>One thing the source could not derive.</summary>
public sealed record PersistedFailure
{
    /// <summary>The type it happened on.</summary>
    [JsonPropertyName("type")]
    public required string TypeName { get; init; }

    /// <summary>The member it happened on, or null.</summary>
    [JsonPropertyName("member")]
    public string? MemberName { get; init; }

    /// <summary>What could not be derived.</summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
