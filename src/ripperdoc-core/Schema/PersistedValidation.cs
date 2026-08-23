using System.Text.Json.Serialization;
using Ripperdoc.Core.Tweak;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// What shipped data said about a schema, in the form it is kept in between
/// runs.
/// </summary>
/// <remarks>
/// Every field is written out, confirmed ones included. The alternative -
/// writing only the fields the data did not confirm and treating the rest as
/// confirmed by their absence - is smaller and states the cornerstone
/// backwards: a field would then be marked validated because nothing said
/// otherwise, which is exactly the reasoning the manifest exists to replace.
/// </remarks>
public sealed record PersistedValidation
{
    /// <summary>What the shipped values were read from.</summary>
    [JsonPropertyName("source")]
    public required string SourceDescription { get; init; }

    /// <summary>How many stored values the schema accounts for.</summary>
    [JsonPropertyName("explained")]
    public required int StoredValuesExplained { get; init; }

    /// <summary>How many stored values the database holds.</summary>
    [JsonPropertyName("storedValues")]
    public required int StoredValueCount { get; init; }

    /// <summary>How many records were examined.</summary>
    [JsonPropertyName("recordsExamined")]
    public required int RecordsExamined { get; init; }

    /// <summary>Record types the database used that the schema does not have.</summary>
    [JsonPropertyName("recordTypesNotInSchema")]
    public required IReadOnlyList<string> RecordTypesNotInSchema { get; init; }

    /// <summary>How many pairs could not be addressed, by reason.</summary>
    [JsonPropertyName("unaddressableByReason")]
    public required IReadOnlyDictionary<string, int> UnaddressableByReason { get; init; }

    /// <summary>The verdict on every field.</summary>
    [JsonPropertyName("fields")]
    public required IReadOnlyList<PersistedFieldValidation> Fields { get; init; }

    /// <summary>Put a manifest into the form it is kept in.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="resolved">
    /// Which name each field is written down under once the data has decided,
    /// or null where nothing decided. A verdict is recorded against the name
    /// the field has in the same document; leaving it under the name the source
    /// guessed would leave the schema and its own verdicts disagreeing about
    /// what a field is called.
    /// </param>
    /// <returns>The persisted form.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    public static PersistedValidation Of(
        ValidationManifest manifest,
        IReadOnlyDictionary<(string Type, string Field), IReadOnlyList<string>>? resolved = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new PersistedValidation
        {
            SourceDescription = manifest.SourceDescription,
            StoredValuesExplained = manifest.StoredValuesExplained,
            StoredValueCount = manifest.StoredValueCount,
            RecordsExamined = manifest.RecordsExamined,
            RecordTypesNotInSchema = manifest.RecordTypesNotInSchema,
            UnaddressableByReason = manifest.UnaddressableFieldProbesByReason
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal),
            Fields = manifest.Fields()
                .Select(field => new PersistedFieldValidation
                {
                    RecordTypeName = field.RecordTypeName,
                    FieldName = SchemaIrDocument.PersistedNameOf(
                        field.DeclaringTypeName,
                        field.FieldName,
                        resolved),
                    StorageType = field.StorageType,
                    DeclaringTypeName = field.DeclaringTypeName,
                    State = field.State.ToString(),
                    CorroboratingValueCount = field.CorroboratingValueCount,
                    ContradictingValueCount = field.ContradictingValueCount,
                    ObservedStorageType = field.ObservedStorageType,
                    Spellings = field.Spellings
                        .Select(spelling => new PersistedSpellingValidation
                        {
                            Name = spelling.Name,
                            State = spelling.State.ToString(),
                            CorroboratingValueCount = spelling.CorroboratingValueCount,
                            ContradictingValueCount = spelling.ContradictingValueCount,
                            ObservedStorageType = spelling.ObservedStorageType,
                        })
                        .ToArray(),
                    ReferentTypeName = field.ReferentTypeName,
                })
                .ToArray(),
        };
    }

    /// <summary>Rebuild the manifest this was written from.</summary>
    /// <returns>The manifest.</returns>
    /// <exception cref="InvalidDataException">
    /// A verdict or a reason names something this build does not know.
    /// </exception>
    public ValidationManifest ToManifest()
    {
        var verdicts = new List<FieldValidation>(Fields.Count);
        foreach (var field in Fields)
        {
            if (!Enum.TryParse<ValidationState>(field.State, out var state))
            {
                throw new InvalidDataException(
                    $"'{field.RecordTypeName}.{field.FieldName}' is recorded as '{field.State}', which is not "
                    + "a verdict this build knows. A field whose verdict cannot be read is not a field that "
                    + "can be reported as validated.");
            }

            var spellings = new List<SpellingValidation>(field.Spellings.Count);
            foreach (var spelling in field.Spellings)
            {
                if (!Enum.TryParse<ValidationState>(spelling.State, out var spellingState))
                {
                    throw new InvalidDataException(
                        $"'{field.RecordTypeName}.{field.FieldName}' under the name '{spelling.Name}' is "
                        + $"recorded as '{spelling.State}', which is not a verdict this build knows. A "
                        + "spelling whose verdict cannot be read is not one that can be reported as "
                        + "confirming anything.");
                }

                spellings.Add(new SpellingValidation(
                    spelling.Name,
                    spellingState,
                    spelling.CorroboratingValueCount,
                    spelling.ContradictingValueCount,
                    spelling.ObservedStorageType));
            }

            verdicts.Add(new FieldValidation(
                field.RecordTypeName,
                field.FieldName,
                field.StorageType,
                field.DeclaringTypeName,
                state,
                field.CorroboratingValueCount,
                field.ContradictingValueCount,
                field.ObservedStorageType,
                spellings,
                field.ReferentTypeName));
        }

        var byReason = new Dictionary<UnaddressableReason, int>();
        foreach (var entry in UnaddressableByReason)
        {
            if (!Enum.TryParse<UnaddressableReason>(entry.Key, out var reason))
            {
                throw new InvalidDataException(
                    $"'{entry.Key}' is not a reason this build knows for a pair having no identifier.");
            }

            byReason[reason] = entry.Value;
        }

        return ValidationManifest.FromRecorded(
            verdicts,
            SourceDescription,
            StoredValuesExplained,
            StoredValueCount,
            RecordsExamined,
            RecordTypesNotInSchema,
            byReason);
    }
}

/// <summary>What shipped data said about one field.</summary>
public sealed record PersistedFieldValidation
{
    /// <summary>The record type the field was checked on.</summary>
    [JsonPropertyName("type")]
    public required string RecordTypeName { get; init; }

    /// <summary>The field's name.</summary>
    [JsonPropertyName("field")]
    public required string FieldName { get; init; }

    /// <summary>The storage type the schema claims.</summary>
    [JsonPropertyName("storageType")]
    public required string StorageType { get; init; }

    /// <summary>The type that declares the field.</summary>
    [JsonPropertyName("declaredBy")]
    public required string DeclaringTypeName { get; init; }

    /// <summary>The verdict.</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>How many values of the claimed type were found.</summary>
    [JsonPropertyName("corroborating")]
    public required int CorroboratingValueCount { get; init; }

    /// <summary>How many values of some other type were found.</summary>
    [JsonPropertyName("contradicting")]
    public required int ContradictingValueCount { get; init; }

    /// <summary>The type of the first contradicting value, or null.</summary>
    [JsonPropertyName("observedStorageType")]
    public string? ObservedStorageType { get; init; }

    /// <summary>
    /// What the data said about each name the field might be stored under.
    /// </summary>
    /// <remarks>
    /// Written out rather than reduced to the names that were confirmed. Which
    /// spelling was contradicted, and by what, is the evidence behind the
    /// field's own verdict, and a reader holding only the confirmed names
    /// cannot tell a field nothing was found for from one whose every candidate
    /// was contradicted.
    /// </remarks>
    [JsonPropertyName("spellings")]
    public required IReadOnlyList<PersistedSpellingValidation> Spellings { get; init; }

    /// <summary>The kind of record the field may point at, or null.</summary>
    [JsonPropertyName("referentType")]
    public string? ReferentTypeName { get; init; }
}

/// <summary>What shipped data said about one spelling of one field.</summary>
public sealed record PersistedSpellingValidation
{
    /// <summary>The spelling probed.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The verdict on this spelling.</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>How many values under this name were of the claimed type.</summary>
    [JsonPropertyName("corroborating")]
    public required int CorroboratingValueCount { get; init; }

    /// <summary>How many were of some other type.</summary>
    [JsonPropertyName("contradicting")]
    public required int ContradictingValueCount { get; init; }

    /// <summary>The type of the first such value, or null.</summary>
    [JsonPropertyName("observedStorageType")]
    public string? ObservedStorageType { get; init; }
}
