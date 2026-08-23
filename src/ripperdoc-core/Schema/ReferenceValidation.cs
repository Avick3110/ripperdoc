using Ripperdoc.Core.Tweak;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// What real stored references say about a reference graph's typed edges.
/// </summary>
/// <remarks>
/// <para>
/// A typed edge claims that values stored in one field name records of one
/// kind. That is a claim about data, and the shipped database is full of the
/// data it is about - so it is checked rather than asserted, the same way every
/// other claim this schema layer makes is.
/// </para>
/// <para>
/// It is also the check that shows the graph is worth having. An edge that
/// survives it lets a later report say an identifier points at the wrong kind
/// of record; an edge that fails it says the derivation is wrong about a field,
/// which is worth knowing before anything is built on it.
/// </para>
/// </remarks>
public sealed class ReferenceValidation
{
    /// <summary>
    /// How many wrongly-typed references are kept as examples.
    /// </summary>
    /// <remarks>
    /// Bounded because the count is the finding and the examples only say where
    /// to start looking. An unbounded list would hold one entry per wrong
    /// reference in a database holding over a million of them.
    /// </remarks>
    public const int ExampleLimit = 50;

    private ReferenceValidation(
        int typedEdgesChecked,
        int untypedEdgesNotChecked,
        int pairsNotAddressable,
        int valuesRead,
        int valuesUnreadable,
        int referencesFollowed,
        int referencesOfPermittedKind,
        int referencesOfOtherKind,
        int referencesToNothing,
        IReadOnlyList<string> recordTypesNotInSchema,
        IReadOnlyList<MistypedReference> examples)
    {
        TypedEdgesChecked = typedEdgesChecked;
        UntypedEdgesNotChecked = untypedEdgesNotChecked;
        PairsNotAddressable = pairsNotAddressable;
        ValuesRead = valuesRead;
        ValuesUnreadable = valuesUnreadable;
        ReferencesFollowed = referencesFollowed;
        ReferencesOfPermittedKind = referencesOfPermittedKind;
        ReferencesOfOtherKind = referencesOfOtherKind;
        ReferencesToNothing = referencesToNothing;
        RecordTypesNotInSchema = recordTypesNotInSchema;
        Examples = examples;
    }

    /// <summary>
    /// How many record-and-field pairs were checked, over every record of every
    /// type carrying a typed edge.
    /// </summary>
    public int TypedEdgesChecked { get; }

    /// <summary>
    /// How many pairs went unchecked because their edge does not say what kind
    /// of record it points at.
    /// </summary>
    /// <remarks>
    /// The whole of a schema read from a compiled type model lands here. It is
    /// counted rather than passed over so that a run against such a schema
    /// reports having checked nothing, instead of reporting no failures.
    /// </remarks>
    public int UntypedEdgesNotChecked { get; }

    /// <summary>
    /// How many pairs went unchecked because no name the field might be stored
    /// under has an identifier on that record.
    /// </summary>
    /// <remarks>
    /// Not folded into <see cref="TypedEdgesChecked"/>, which says how many
    /// pairs were actually looked at. A pair with no identifier under any of its
    /// spellings was not looked at - there was nowhere to look - and counting it
    /// as checked reports a check that did not happen, which is the failure the
    /// validation manifest gives its own state to and this had none for.
    /// Expected to be zero.
    /// </remarks>
    public int PairsNotAddressable { get; }

    /// <summary>
    /// Record types the records carry that the schema has never heard of, in a
    /// stable order.
    /// </summary>
    /// <remarks>
    /// A record of such a type has no edges here, and so contributes nothing to
    /// any count below - indistinguishable, without this, from a record of a
    /// known type that holds no references. The first was examined against no
    /// schema at all and the second was examined and found clean, and a run that
    /// reported them alike would be reporting coverage it did not have.
    /// </remarks>
    public IReadOnlyList<string> RecordTypesNotInSchema { get; }

    /// <summary>How many stored values were found and read as references.</summary>
    public int ValuesRead { get; }

    /// <summary>
    /// How many stored values were found under a typed edge and could not be
    /// read as references.
    /// </summary>
    /// <remarks>
    /// A value the schema calls a reference and the database stores as
    /// something else. The same disagreement the validation manifest reports as
    /// a contradiction, seen from the other side.
    /// </remarks>
    public int ValuesUnreadable { get; }

    /// <summary>How many individual references were followed.</summary>
    public int ReferencesFollowed { get; }

    /// <summary>
    /// How many pointed at a record of the kind the edge permits, or of a kind
    /// deriving from it.
    /// </summary>
    public int ReferencesOfPermittedKind { get; }

    /// <summary>How many pointed at a record of some other kind.</summary>
    public int ReferencesOfOtherKind { get; }

    /// <summary>
    /// How many pointed at no record in this database at all.
    /// </summary>
    /// <remarks>
    /// Not a failure of the edge. An identifier naming nothing is a fact about
    /// the data - a reference into content that is not shipped - and it is
    /// counted apart from a reference that points at the wrong kind of thing,
    /// because only the second says the schema is wrong.
    /// </remarks>
    public int ReferencesToNothing { get; }

    /// <summary>
    /// Up to <see cref="ExampleLimit"/> references that pointed at a record of
    /// some other kind.
    /// </summary>
    public IReadOnlyList<MistypedReference> Examples { get; }

    /// <summary>
    /// Whether every reference that was followed pointed at a permitted kind of
    /// record.
    /// </summary>
    /// <remarks>
    /// True of a run that followed nothing, which is why this is never read
    /// without <see cref="ReferencesFollowed"/> beside it. A graph with no
    /// typed edge at all cannot be contradicted, and reporting that as the
    /// graph being right would be the failure this layer exists to prevent.
    /// </remarks>
    public bool NothingContradictsTheGraph => ReferencesOfOtherKind == 0;

    /// <summary>
    /// Follow every reference stored under a typed edge and check what kind of
    /// record it names.
    /// </summary>
    /// <param name="graph">The graph whose edges are being checked.</param>
    /// <param name="shipped">The records to check over.</param>
    /// <param name="values">Where a stored value's references are read from.</param>
    /// <returns>What the data said.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ReferenceValidation Build(
        ReferenceGraph graph,
        IShippedRecordSource shipped,
        IStoredReferenceSource values)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(shipped);
        ArgumentNullException.ThrowIfNull(values);

        // Built once, before anything is followed. Every reference needs the
        // kind of the record it names, and asking the database that question
        // per reference is the shape of call this engine has already measured
        // as thousands of times the cost of a lookup.
        var kindOf = new Dictionary<ulong, string>();
        foreach (var record in shipped.Records)
        {
            kindOf[record.Identifier] = record.TypeName;
        }

        var typedChecked = 0;
        var untypedSkipped = 0;
        var notAddressable = 0;
        var unknownTypes = new HashSet<string>(StringComparer.Ordinal);
        var read = 0;
        var unreadable = 0;
        var followed = 0;
        var permitted = 0;
        var other = 0;
        var toNothing = 0;
        var examples = new List<MistypedReference>();

        foreach (var record in shipped.Records)
        {
            // A record of a type the schema never heard of has no edges here,
            // so without this it would slip through contributing to nothing and
            // be indistinguishable from a record examined and found clean.
            if (!graph.DescribesRecordType(record.TypeName))
            {
                unknownTypes.Add(record.TypeName);
                continue;
            }

            foreach (var edge in graph.From(record.TypeName))
            {
                if (edge.ReferentTypeName is null)
                {
                    untypedSkipped++;
                    continue;
                }

                // Counted after the probing rather than before it. A pair whose
                // every spelling is unaddressable was never looked at, and
                // calling it checked would report a check that could not happen.
                var addressed = false;

                // Every spelling the schema offers for the field, not only the
                // one it keys the field by. A schema derived from accessor
                // shapes cannot recover a name's capitalisation and carries
                // both, and shipped data really does use either - so a
                // primary-name probe would find nothing for a field whose
                // values are all there under the other spelling, and this whole
                // check would report a clean run having examined none of them.
                foreach (var candidate in edge.CandidateFieldNames)
                {
                    if (!TweakIdentifier.TryForField(record.Identifier, candidate, out var identifier, out _))
                    {
                        continue;
                    }

                    addressed = true;

                    if (!values.TryGetStoredIdentifiers(identifier, out var targets))
                    {
                        // Absent and unreadable are told apart here rather than
                        // together: a field a record simply does not carry is the
                        // ordinary case, and a value that is there and is not a
                        // reference is a disagreement worth counting.
                        if (shipped.TryGetStoredValueType(identifier, out _))
                        {
                            unreadable++;
                        }

                        continue;
                    }

                    read++;

                    foreach (var target in targets)
                    {
                        followed++;

                        if (!kindOf.TryGetValue(target, out var actual))
                        {
                            toNothing++;
                            continue;
                        }

                        if (graph.Permits(edge.ReferentTypeName, actual))
                        {
                            permitted++;
                            continue;
                        }

                        other++;
                        if (examples.Count < ExampleLimit)
                        {
                            examples.Add(new MistypedReference(
                                record.TypeName,
                                candidate,
                                edge.ReferentTypeName,
                                actual));
                        }
                    }
                }

                if (addressed)
                {
                    typedChecked++;
                }
                else
                {
                    notAddressable++;
                }
            }
        }

        return new ReferenceValidation(
            typedChecked,
            untypedSkipped,
            notAddressable,
            read,
            unreadable,
            followed,
            permitted,
            other,
            toNothing,
            unknownTypes.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            examples);
    }
}

/// <summary>
/// One stored reference that named a record of a kind its field does not
/// permit.
/// </summary>
/// <param name="RecordTypeName">The record type the reference was stored on.</param>
/// <param name="FieldName">The field holding it.</param>
/// <param name="PermittedTypeName">The kind of record the schema permits there.</param>
/// <param name="ActualTypeName">The kind of record it actually named.</param>
/// <remarks>
/// The records themselves are not named. What is wrong is the same for every
/// record of the type, and naming one would put a shipped identifier into a
/// result that has no need of it.
/// </remarks>
public sealed record MistypedReference(
    string RecordTypeName,
    string FieldName,
    string PermittedTypeName,
    string ActualTypeName);
