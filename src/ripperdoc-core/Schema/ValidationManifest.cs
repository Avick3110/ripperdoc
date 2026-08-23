using Ripperdoc.Core.Tweak;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// Every field in a schema, marked against what a real shipped database
/// actually holds.
/// </summary>
/// <remarks>
/// <para>
/// This is the no-silent-failure rule in structural form. A schema derived
/// from a type model is a claim about what exists; the shipped database is the
/// only thing that can confirm it. Rather than assume the claim and be
/// confidently wrong about a field forever, every field is checked and the
/// ones that could not be confirmed are labelled as such - including the ones
/// that could not be confirmed because there was nothing to check them
/// against, which is a different thing from being wrong and is recorded as a
/// different thing.
/// </para>
/// <para>
/// The check needs the schema, the database, and identifier arithmetic. It
/// does not need type information generated from a game install, which is why
/// the no-setup mode can audit its own schema instead of asking to be trusted.
/// </para>
/// </remarks>
public sealed class ValidationManifest
{
    private readonly IReadOnlyList<FieldValidation> _fields;

    private ValidationManifest(
        IReadOnlyList<FieldValidation> fields,
        string sourceDescription,
        int storedValuesExplained,
        int storedValueCount,
        int recordsExamined,
        IReadOnlyList<string> recordTypesNotInSchema,
        int unaddressableFieldProbes,
        IReadOnlyDictionary<UnaddressableReason, int> unaddressableFieldProbesByReason)
    {
        _fields = fields;
        SourceDescription = sourceDescription;
        StoredValuesExplained = storedValuesExplained;
        StoredValueCount = storedValueCount;
        RecordsExamined = recordsExamined;
        RecordTypesNotInSchema = recordTypesNotInSchema;
        UnaddressableFieldProbes = unaddressableFieldProbes;
        UnaddressableFieldProbesByReason = unaddressableFieldProbesByReason;
    }

    /// <summary>What the shipped values were read from.</summary>
    public string SourceDescription { get; }

    /// <summary>
    /// How many distinct stored values the schema accounts for.
    /// </summary>
    public int StoredValuesExplained { get; }

    /// <summary>How many stored values the database holds in total.</summary>
    public int StoredValueCount { get; }

    /// <summary>How many records were examined.</summary>
    public int RecordsExamined { get; }

    /// <summary>
    /// Record types the database uses that the schema has never heard of, in a
    /// stable order.
    /// </summary>
    /// <remarks>
    /// Expected to be empty against a database the schema's type model covers.
    /// A name here means records of that type were examined against no schema
    /// at all, so it is reported rather than folded into the unexplained count
    /// where it would look like ordinary residue.
    /// </remarks>
    public IReadOnlyList<string> RecordTypesNotInSchema { get; }

    /// <summary>
    /// How many record-and-field pairs could not be addressed at all, for any
    /// of the reasons a pair can have no identifier.
    /// </summary>
    /// <remarks>
    /// Expected to be zero. Such a pair is not a failure of the sweep - no
    /// stored value can exist under a name with no identifier - but it is a
    /// place the sweep looked and could not look properly, so it is counted
    /// rather than passed over in silence. Which reasons, and how many of each,
    /// is in <see cref="UnaddressableFieldProbesByReason"/>: a total on its own
    /// invites whoever reads it to assume the reason that comes to mind.
    /// </remarks>
    public int UnaddressableFieldProbes { get; }

    /// <summary>
    /// How many pairs could not be addressed for each reason.
    /// </summary>
    /// <remarks>
    /// Every reason appears, including those nothing hit, so a reader can see
    /// which were looked for as well as which were found.
    /// <see cref="UnaddressableReason.None"/> is not among them: it names a
    /// pair that has an identifier, which is not a reason for anything.
    /// </remarks>
    public IReadOnlyDictionary<UnaddressableReason, int> UnaddressableFieldProbesByReason { get; }

    /// <summary>
    /// The share of stored values the schema accounts for, between 0 and 1.
    /// </summary>
    /// <remarks>
    /// A database holding no values at all reports zero, which reads as "none
    /// of it was explained" rather than "there was nothing to explain". The
    /// two are told apart by <see cref="StoredValueCount"/>, which a caller has
    /// in hand; no branch is spent here distinguishing a case that no arbiter
    /// produces.
    /// </remarks>
    public double ExplainedShare => StoredValueCount == 0
        ? 0d
        : (double)StoredValuesExplained / StoredValueCount;

    /// <summary>Every field, in a stable order.</summary>
    /// <returns>The per-field verdicts.</returns>
    public IReadOnlyList<FieldValidation> Fields() => _fields;

    /// <summary>Every field that a shipped value confirms.</summary>
    /// <returns>The confirmed fields.</returns>
    public IEnumerable<FieldValidation> Validated() => _fields.Where(field => field.IsValidated);

    /// <summary>Every field that no shipped value confirms.</summary>
    /// <returns>The unconfirmed fields, whatever the reason.</returns>
    public IEnumerable<FieldValidation> Unvalidated() => _fields.Where(field => !field.IsValidated);

    /// <summary>How many fields are in each state.</summary>
    /// <returns>A count per state, including states with no fields in them.</returns>
    public IReadOnlyDictionary<ValidationState, int> StateCounts()
    {
        var counts = Enum.GetValues<ValidationState>().ToDictionary(state => state, _ => 0);
        foreach (var field in _fields)
        {
            counts[field.State]++;
        }

        return counts;
    }

    /// <summary>
    /// Check every field of every record type in <paramref name="schema"/>
    /// against <paramref name="shipped"/>.
    /// </summary>
    /// <param name="schema">The schema whose claims are being checked.</param>
    /// <param name="shipped">The database that arbitrates them.</param>
    /// <returns>The manifest.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ValidationManifest Build(RecordSchema schema, IShippedRecordSource shipped)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(shipped);

        var tallies = new Dictionary<string, Dictionary<string, Tally>>(StringComparer.Ordinal);
        var recordsPerType = new Dictionary<string, int>(StringComparer.Ordinal);
        var unknownTypes = new HashSet<string>(StringComparer.Ordinal);
        var explained = new HashSet<ulong>();
        var recordsExamined = 0;
        var unaddressable = 0;
        var unaddressableByReason = Enum.GetValues<UnaddressableReason>()
            .Where(value => value != UnaddressableReason.None)
            .ToDictionary(value => value, _ => 0);

        foreach (var record in shipped.Records)
        {
            recordsExamined++;
            recordsPerType[record.TypeName] = recordsPerType.GetValueOrDefault(record.TypeName) + 1;

            var type = schema.Find(record.TypeName);
            if (type is null)
            {
                unknownTypes.Add(record.TypeName);
                continue;
            }

            if (!tallies.TryGetValue(record.TypeName, out var fieldTallies))
            {
                tallies[record.TypeName] = fieldTallies = new Dictionary<string, Tally>(StringComparer.Ordinal);
            }

            foreach (var field in type.Fields.Values)
            {
                if (!fieldTallies.TryGetValue(field.Name, out var tally))
                {
                    fieldTallies[field.Name] = tally = new Tally();
                }

                // Every spelling the field might be stored under is probed, and
                // the outcomes are tallied against the one field rather than
                // against each spelling. A source that cannot recover the
                // capitalisation of a name is describing one field it is unsure
                // how to spell, not several fields - so counting each candidate
                // as its own field would inflate the schema with slots that
                // were never claimed to exist.
                foreach (var candidate in field.CandidateNames())
                {
                    if (!TweakIdentifier.TryForField(record.Identifier, candidate, out var identifier, out var reason))
                    {
                        // No identifier exists for this pair, so there is
                        // nothing to look under. Recorded as its own outcome:
                        // marking it the same way as a field the records were
                        // checked for and did not carry would claim a check that
                        // never happened. The reason is kept beside the count
                        // because the three reasons send a reader to three
                        // different places.
                        unaddressable++;
                        unaddressableByReason[reason]++;
                        tally.Unaddressable++;
                        continue;
                    }

                    if (!shipped.TryGetStoredValueType(identifier, out var storedType))
                    {
                        continue;
                    }

                    explained.Add(identifier);

                    if (storedType is null)
                    {
                        tally.Unreadable++;
                    }
                    else if (string.Equals(storedType, field.StorageType, StringComparison.Ordinal))
                    {
                        tally.Agreeing++;
                        tally.Confirmed.Add(candidate);
                    }
                    else
                    {
                        tally.Disagreeing++;
                        tally.ObservedStorageType ??= storedType;
                    }
                }
            }
        }

        var verdicts = new List<FieldValidation>();
        foreach (var typeName in schema.RecordTypeNames)
        {
            var type = schema.Find(typeName)!;
            var hasRecords = recordsPerType.GetValueOrDefault(typeName) > 0;
            tallies.TryGetValue(typeName, out var fieldTallies);

            foreach (var field in type.Fields.Values.OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                var tally = fieldTallies?.GetValueOrDefault(field.Name) ?? Tally.Empty;
                verdicts.Add(new FieldValidation(
                    typeName,
                    field.Name,
                    field.StorageType,
                    field.DeclaringTypeName,
                    StateOf(tally, hasRecords),
                    tally.Agreeing,
                    tally.Disagreeing,
                    tally.ObservedStorageType,
                    tally.Confirmed.ToArray(),
                    field.ReferentTypeName));
            }
        }

        return new ValidationManifest(
            verdicts,
            shipped.Description,
            explained.Count,
            shipped.StoredValueCount,
            recordsExamined,
            unknownTypes.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            unaddressable,
            unaddressableByReason);
    }

    private static ValidationState StateOf(Tally tally, bool typeHasRecords)
    {
        // Ordered by how much each outcome establishes. A contradiction is the
        // strongest thing the data can say, and the weakest - that nothing
        // could be looked at - must never be reported as one of the others.
        // Disagreement wins over agreement. One stored value of the wrong type
        // is a finding about the schema; it is not outvoted by the values that
        // happened to match.
        if (tally.Disagreeing > 0)
        {
            return ValidationState.Contradicted;
        }

        if (tally.Agreeing > 0)
        {
            return ValidationState.Corroborated;
        }

        if (tally.Unreadable > 0)
        {
            return ValidationState.StorageTypeUnreadable;
        }

        if (tally.Unaddressable > 0)
        {
            return ValidationState.NotAddressable;
        }

        return typeHasRecords
            ? ValidationState.NoCorroboratingValue
            : ValidationState.NoShippedRecordsOfType;
    }

    private sealed class Tally
    {
        /// <summary>
        /// The tally of a field nothing was recorded against - a type with no
        /// shipped records of it at all. Shared rather than allocated per
        /// field, and never written to, because the reporting pass only reads.
        /// </summary>
        public static readonly Tally Empty = new();

        public int Agreeing;
        public int Disagreeing;
        public int Unreadable;
        public int Unaddressable;
        public string? ObservedStorageType;

        /// <summary>
        /// The spellings a corroborating value was actually found under, in a
        /// stable order.
        /// </summary>
        public SortedSet<string> Confirmed { get; } = new(StringComparer.Ordinal);
    }
}

/// <summary>
/// What a shipped database had to say about one field of one record type.
/// </summary>
/// <param name="RecordTypeName">The record type the field was checked on.</param>
/// <param name="FieldName">The field's name.</param>
/// <param name="StorageType">The storage type the schema claims.</param>
/// <param name="DeclaringTypeName">
/// The type that declares the field, which may be an ancestor of
/// <paramref name="RecordTypeName"/>.
/// </param>
/// <param name="State">The verdict.</param>
/// <param name="CorroboratingValueCount">
/// How many stored values were found for this field, of the claimed type.
/// </param>
/// <param name="ContradictingValueCount">
/// How many stored values were found for this field, of some other type.
/// </param>
/// <param name="ObservedStorageType">
/// The storage type of the first contradicting value, or null where there was
/// none.
/// </param>
/// <param name="ConfirmedFieldNames">
/// The spellings of the field name that stored values were actually found
/// under, in a stable order. Empty where none was.
/// </param>
/// <param name="ReferentTypeName">
/// The kind of record this field's stored identifier points at, or null where
/// the schema does not say.
/// </param>
/// <remarks>
/// <see cref="ConfirmedFieldNames"/> is a list rather than a name because a
/// schema derived from accessor shapes offers more than one spelling of the
/// same field and the data decides between them. It says which spelling was
/// vindicated - and if more than one was, it says that too rather than picking
/// one and reporting a certainty nothing established.
/// </remarks>
public sealed record FieldValidation(
    string RecordTypeName,
    string FieldName,
    string StorageType,
    string DeclaringTypeName,
    ValidationState State,
    int CorroboratingValueCount,
    int ContradictingValueCount,
    string? ObservedStorageType,
    IReadOnlyList<string> ConfirmedFieldNames,
    string? ReferentTypeName)
{
    /// <summary>
    /// Whether real shipped data confirms this field.
    /// </summary>
    /// <remarks>
    /// True for exactly one state. Everything else is a field the schema
    /// claims and the data does not confirm, and the reasons differ enough to
    /// be worth reading separately - which is what <see cref="State"/> is for.
    /// </remarks>
    public bool IsValidated => State == ValidationState.Corroborated;
}

/// <summary>
/// What a shipped database was able to say about a schema field.
/// </summary>
public enum ValidationState
{
    /// <summary>
    /// Stored values exist for this field and every one of them is stored as
    /// the type the schema claims.
    /// </summary>
    Corroborated,

    /// <summary>
    /// A stored value exists for this field and is stored as some other type.
    /// The schema is wrong about it, and saying so is the point of checking.
    /// </summary>
    Contradicted,

    /// <summary>
    /// The record type has shipped records and none of them carries this field.
    /// The field may be correct and unused, or it may not exist; the data
    /// cannot tell the two apart, so neither does this.
    /// </summary>
    NoCorroboratingValue,

    /// <summary>
    /// The record type has no shipped records at all, so nothing could have
    /// confirmed the field either way.
    /// </summary>
    NoShippedRecordsOfType,

    /// <summary>
    /// Stored values exist for this field but the database could not say what
    /// type they are stored as, so the claim is neither confirmed nor denied.
    /// </summary>
    StorageTypeUnreadable,

    /// <summary>
    /// This field has no identifier on records of this type, so nothing could
    /// be stored under it and nothing was looked for.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="NoCorroboratingValue"/>, which says the
    /// records were checked and did not carry the field. Here they were not
    /// checked, because there was no identifier to check under - and reporting
    /// a check that did not happen is the failure this whole manifest exists to
    /// prevent.
    /// </remarks>
    NotAddressable,
}
