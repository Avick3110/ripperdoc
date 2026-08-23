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
    /// <remarks>
    /// <para>
    /// Counted under the names the arbitrated schema keys its fields by, which
    /// are not always the names it was asked about. A field whose spelling the
    /// source could not recover is probed under every candidate, and where the
    /// data confirms one of them the others are guesses the data rejected -
    /// what sits at a rejected guess's identifier belongs to whatever else that
    /// identifier addresses, and counting it here would credit the schema with
    /// explaining a value it does not describe.
    /// </para>
    /// <para>
    /// Where no candidate was confirmed, nothing has decided between them, so
    /// all of them still count - which is the same set of names the artifact
    /// goes on carrying for such a field. The number therefore says what the
    /// schema as it would be written down accounts for, rather than what the
    /// widest possible probe happened to reach.
    /// </para>
    /// </remarks>
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
    /// How many attempts to address a field on a record found no identifier, for
    /// any of the reasons there can be none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Counted per attempt: once per record, per name the field might be stored
    /// under. A field the source offers two spellings of contributes two on
    /// every record of its type, so three such records report six - and this is
    /// a count of probes, not of fields, of names, or of record-and-field pairs.
    /// </para>
    /// <para>
    /// The unit is the probe because the probe is what could not be made, and
    /// whether it can be depends on the record as much as on the name: a record
    /// whose own name is long leaves no room for a field name beside it inside
    /// an identifier, while the same field is perfectly addressable on a
    /// shorter-named record of the same type. There is no smaller unit that
    /// could carry that - a count of names would have to say a name is
    /// unaddressable when it is only unaddressable somewhere.
    /// </para>
    /// <para>
    /// Expected to be zero. Such a probe is not a failure of the sweep - no
    /// stored value can exist under a name with no identifier - but it is a
    /// place the sweep looked and could not look properly, so it is counted
    /// rather than passed over in silence. Which reasons, and how many of each,
    /// is in <see cref="UnaddressableFieldProbesByReason"/>: a total on its own
    /// invites whoever reads it to assume the reason that comes to mind.
    /// </para>
    /// </remarks>
    public int UnaddressableFieldProbes { get; }

    /// <summary>
    /// How many such probes were refused for each reason.
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
    /// Rebuild a manifest from verdicts that were recorded earlier.
    /// </summary>
    /// <param name="fields">The per-field verdicts.</param>
    /// <param name="sourceDescription">What arbitrated the schema.</param>
    /// <param name="storedValuesExplained">How many stored values it accounted for.</param>
    /// <param name="storedValueCount">How many the database held.</param>
    /// <param name="recordsExamined">How many records were examined.</param>
    /// <param name="recordTypesNotInSchema">Record types the schema did not have.</param>
    /// <param name="unaddressableFieldProbesByReason">
    /// How many attempts to address a field on a record found no identifier,
    /// per reason.
    /// </param>
    /// <returns>The manifest.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <remarks>
    /// For reading back a manifest that was written out, and for nothing else.
    /// It does no checking of its own - it restates verdicts a run of
    /// <see cref="Build"/> already reached - which is why it takes them whole
    /// rather than recomputing anything from them.
    /// </remarks>
    public static ValidationManifest FromRecorded(
        IReadOnlyList<FieldValidation> fields,
        string sourceDescription,
        int storedValuesExplained,
        int storedValueCount,
        int recordsExamined,
        IReadOnlyList<string> recordTypesNotInSchema,
        IReadOnlyDictionary<UnaddressableReason, int> unaddressableFieldProbesByReason)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(sourceDescription);
        ArgumentNullException.ThrowIfNull(recordTypesNotInSchema);
        ArgumentNullException.ThrowIfNull(unaddressableFieldProbesByReason);

        return new ValidationManifest(
            fields,
            sourceDescription,
            storedValuesExplained,
            storedValueCount,
            recordsExamined,
            recordTypesNotInSchema,
            unaddressableFieldProbesByReason.Values.Sum(),
            unaddressableFieldProbesByReason);
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

        // Tallied per spelling, not per field. A field the source offers more
        // than one spelling of is one field the data is asked about several
        // ways, and the answers differ: run together, a value sitting at a
        // guessed spelling's identifier is evidence about the field, and the
        // field is condemned for what some unrelated value happens to be. Kept
        // apart, the guess is what the contradiction is about.
        var tallies = new Dictionary<string, Dictionary<(string Field, string Spelling), Tally>>(
            StringComparer.Ordinal);
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
                tallies[record.TypeName] = fieldTallies =
                    new Dictionary<(string Field, string Spelling), Tally>();
            }

            foreach (var field in type.Fields.Values)
            {
                // Every spelling the schema offers for this field on this type,
                // and no spelling that is really another field's name. Asking
                // for them here rather than reading the field's own alternates
                // is what keeps that exclusion in one place.
                var candidates = type.Spellings.Of(field);

                // Whether this field's spellings are still in question. Where
                // they are, what was found under each is kept apart until the
                // verdicts are in, because which of them the schema ends up
                // keyed by is not known until then. Where the source knew the
                // name there is nothing to decide, so those go straight in and
                // no list is allocated for them - which is every field of a
                // schema read from a compiled type model.
                var contested = candidates.Count > 1;

                foreach (var candidate in candidates)
                {
                    if (!fieldTallies.TryGetValue((field.Name, candidate), out var tally))
                    {
                        fieldTallies[(field.Name, candidate)] = tally = new Tally();
                    }

                    if (!TweakIdentifier.TryForField(record.Identifier, candidate, out var identifier, out var reason))
                    {
                        // No identifier exists for this name on this record,
                        // so there is nothing to look under. Recorded as its own
                        // outcome: marking it the same way as a field the
                        // records were checked for and did not carry would claim
                        // a check that never happened. Counted once here, which
                        // is once per record and per name - the loop this sits
                        // in runs for both - because that is the unit of the
                        // thing that could not be done. The reason is kept
                        // beside the count because the three reasons send a
                        // reader to three different places.
                        unaddressable++;
                        unaddressableByReason[reason]++;
                        tally.Unaddressable++;
                        continue;
                    }

                    if (!shipped.TryGetStoredValueType(identifier, out var storedType))
                    {
                        continue;
                    }

                    if (contested)
                    {
                        (tally.Found ??= new List<ulong>()).Add(identifier);
                    }
                    else
                    {
                        explained.Add(identifier);
                    }

                    if (storedType is null)
                    {
                        tally.Unreadable++;
                    }
                    else if (string.Equals(storedType, field.StorageType, StringComparison.Ordinal))
                    {
                        tally.Agreeing++;
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
                var spellings = type.Spellings.Of(field)
                    .Select(candidate =>
                    {
                        var tally = fieldTallies?.GetValueOrDefault((field.Name, candidate)) ?? Tally.Empty;
                        return new SpellingValidation(
                            candidate,
                            StateOf(tally, hasRecords),
                            tally.Agreeing,
                            tally.Disagreeing,
                            tally.ObservedStorageType);
                    })
                    .ToArray();

                var state = StateOfField(spellings, hasRecords);

                // Now that the data has spoken, the values reached under the
                // spellings it settled on are the ones this schema accounts
                // for. A confirmed spelling settles the question the candidates
                // existed to ask, so the rest are guesses the data rejected and
                // what sits at their identifiers is somebody else's. Where it
                // confirmed none, nothing has decided, and every candidate
                // still counts - the same ones the artifact goes on carrying.
                if (spellings.Length > 1)
                {
                    var settled = spellings.Any(spelling => spelling.State == ValidationState.Corroborated)
                        ? spellings.Where(spelling => spelling.State == ValidationState.Corroborated)
                        : spellings;

                    foreach (var spelling in settled)
                    {
                        var found = fieldTallies?.GetValueOrDefault((field.Name, spelling.Name))?.Found;
                        if (found is null)
                        {
                            continue;
                        }

                        foreach (var identifier in found)
                        {
                            explained.Add(identifier);
                        }
                    }
                }

                verdicts.Add(new FieldValidation(
                    typeName,
                    field.Name,
                    field.StorageType,
                    field.DeclaringTypeName,
                    state,
                    spellings.Sum(spelling => spelling.CorroboratingValueCount),
                    spellings.Sum(spelling => spelling.ContradictingValueCount),
                    state == ValidationState.Contradicted
                        ? spellings.FirstOrDefault(spelling => spelling.State == ValidationState.Contradicted)
                            ?.ObservedStorageType
                        : null,
                    spellings,
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

    /// <summary>
    /// The verdict on a field, from the verdicts on the names it might be
    /// stored under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A spelling the data confirms settles the question the alternates existed
    /// to ask. Once one name is known to be the one values are keyed by, what
    /// sits at another candidate's identifier is that candidate's business and
    /// says nothing about this field - so corroboration under any spelling
    /// outranks contradiction under another, and only here.
    /// </para>
    /// <para>
    /// Within a single spelling the order is the other way round, and stays
    /// that way: one value of the wrong type under the name a field really is
    /// keyed by is a finding about the schema, and is not outvoted by the
    /// values that matched. So a field with one spelling - which is every field
    /// of a schema read from a compiled type model - is judged exactly as it
    /// was before there were any alternates to weigh.
    /// </para>
    /// <para>
    /// Where no spelling is confirmed, a contradiction under any of them stands
    /// against the field. Nothing has established which name is real, so there
    /// is no ground for calling the contradiction somebody else's.
    /// </para>
    /// </remarks>
    private static ValidationState StateOfField(
        IReadOnlyList<SpellingValidation> spellings,
        bool typeHasRecords)
    {
        foreach (var ranked in RankedStates)
        {
            if (spellings.Any(spelling => spelling.State == ranked))
            {
                return ranked;
            }
        }

        return typeHasRecords
            ? ValidationState.NoCorroboratingValue
            : ValidationState.NoShippedRecordsOfType;
    }

    private static readonly ValidationState[] RankedStates =
    [
        ValidationState.Corroborated,
        ValidationState.Contradicted,
        ValidationState.StorageTypeUnreadable,
        ValidationState.NotAddressable,
    ];

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
        /// The identifiers stored values were found under, held back until the
        /// verdicts decide whether this spelling is one the schema is keyed by.
        /// Null for a field whose name the source knew, because there is
        /// nothing to decide and those identifiers are counted as they are met.
        /// </summary>
        public List<ulong>? Found;
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
/// How many stored values of the claimed type were found, added up over every
/// spelling probed.
/// </param>
/// <param name="ContradictingValueCount">
/// How many stored values of some other type were found, added up over every
/// spelling probed. Can be non-zero on a corroborated field: the contradiction
/// then sits under a spelling the data rejected, and
/// <paramref name="Spellings"/> says which.
/// </param>
/// <param name="ObservedStorageType">
/// The storage type of the first contradicting value, or null where the field
/// is not contradicted. Deliberately null rather than reported from a rejected
/// spelling - a type named here is read as the type this field's values really
/// have.
/// </param>
/// <param name="Spellings">
/// What the data said about each name the field might be stored under, in probe
/// order. One entry where the source knew the name; more where it did not, and
/// the entries are then how the two are told apart.
/// </param>
/// <param name="ReferentTypeName">
/// The kind of record this field's stored identifier points at, or null where
/// the schema does not say.
/// </param>
/// <remarks>
/// The per-spelling entries are what makes "confirmed under its own name and
/// contradicted under a guess" something this can say. Tallied into one field
/// it is not sayable at all, and the field is reported as contradicted on
/// evidence about a name it never had.
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
    IReadOnlyList<SpellingValidation> Spellings,
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

    /// <summary>
    /// The spellings the data vindicated, in probe order. Empty where it
    /// vindicated none.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Spellings"/> rather than recorded beside it, so
    /// that "which spelling was confirmed" and "what the data said about each
    /// spelling" cannot come to disagree. A spelling is vindicated when values
    /// were found under it and every one of them was of the claimed type; one
    /// that also holds a value of another type has not been shown to be this
    /// field's name.
    /// </remarks>
    public IEnumerable<string> ConfirmedFieldNames => Spellings
        .Where(spelling => spelling.State == ValidationState.Corroborated)
        .Select(spelling => spelling.Name);
}

/// <summary>
/// What a shipped database had to say about one name a field might be stored
/// under.
/// </summary>
/// <param name="Name">The spelling probed.</param>
/// <param name="State">
/// The verdict on this spelling alone, reached by the same rule that once
/// judged a whole field: a contradiction under a name outranks agreement under
/// the same name.
/// </param>
/// <param name="CorroboratingValueCount">
/// How many stored values under this name were of the claimed type.
/// </param>
/// <param name="ContradictingValueCount">
/// How many were of some other type.
/// </param>
/// <param name="ObservedStorageType">
/// The type of the first such value, or null where there was none.
/// </param>
public sealed record SpellingValidation(
    string Name,
    ValidationState State,
    int CorroboratingValueCount,
    int ContradictingValueCount,
    string? ObservedStorageType);

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
