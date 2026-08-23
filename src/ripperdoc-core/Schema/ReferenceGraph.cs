namespace Ripperdoc.Core.Schema;

/// <summary>
/// Which record types point at which, recovered from a schema.
/// </summary>
/// <remarks>
/// <para>
/// A record that refers to another stores an identifier, and an identifier says
/// which record is pointed at but not what kind of record was allowed there. So
/// a reference can always be checked for existence and can only sometimes be
/// checked for kind - and which of those a caller gets depends on where the
/// schema came from. That is the whole reason this is a type of its own rather
/// than a query over the schema: the difference between the two modes is a
/// property of this graph, counted here, rather than a caveat in a document.
/// </para>
/// <para>
/// What it is for downstream: an edge is the statement that values stored in
/// this field must name a record of that kind. It turns "this identifier points
/// at nothing" - which either mode can say - into "this identifier points at
/// the wrong kind of record", which only a typed edge can say. A report that
/// can name the second is the difference between telling somebody a value is
/// broken and telling them why.
/// </para>
/// </remarks>
public sealed class ReferenceGraph
{
    private readonly ILookup<string, ReferenceEdge> _bySource;
    private readonly RecordSchema _schema;
    private readonly Dictionary<string, string?> _parentOf;

    private ReferenceGraph(RecordSchema schema, IReadOnlyList<ReferenceEdge> edges)
    {
        _schema = schema;

        // Every type the schema carries, not only the record types. Walking a
        // chain through the record types alone stops at the first ancestor that
        // is not one - and stopping is indistinguishable from arriving at the
        // top, so a kind that really does derive from the permitted one would be
        // reported as unrelated to it.
        _parentOf = schema.AllTypes().ToDictionary(
            type => type.Name,
            type => type.BaseTypeName,
            StringComparer.Ordinal);

        _bySource = edges.ToLookup(edge => edge.RecordTypeName, StringComparer.Ordinal);
        Edges = edges;
        ReferentTypeNames = edges
            .Where(edge => edge.ReferentTypeName is not null)
            .Select(edge => edge.ReferentTypeName!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Every field slot on every record type whose value is a reference, in a
    /// stable order.
    /// </summary>
    public IReadOnlyList<ReferenceEdge> Edges { get; }

    /// <summary>
    /// Every kind of record something in this schema points at, in a stable
    /// order. Empty where no edge is typed.
    /// </summary>
    public IReadOnlyList<string> ReferentTypeNames { get; }

    /// <summary>How many edges say what kind of record they point at.</summary>
    public int TypedEdgeCount => Edges.Count(edge => edge.ReferentTypeName is not null);

    /// <summary>
    /// How many edges are references whose permitted kind the schema does not
    /// know.
    /// </summary>
    /// <remarks>
    /// This is the shortfall of the inherited mode stated as a number rather
    /// than as a sentence: every edge it produces is one of these, because the
    /// compiled type model records the storage type of a reference and nothing
    /// about its target.
    /// </remarks>
    public int UntypedEdgeCount => Edges.Count(edge => edge.ReferentTypeName is null);

    /// <summary>
    /// Recover the graph from a schema.
    /// </summary>
    /// <param name="schema">The schema to read edges from.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    public static ReferenceGraph Of(RecordSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var edges = new List<ReferenceEdge>();

        foreach (var typeName in schema.RecordTypeNames)
        {
            var type = schema.Find(typeName)!;

            foreach (var field in type.Fields.Values.OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                var depth = SequenceDepthOfReference(field.StorageType);
                if (depth < 0)
                {
                    continue;
                }

                edges.Add(new ReferenceEdge(
                    typeName,
                    field.Name,
                    field.ReferentTypeName,
                    depth > 0,
                    field.DeclaringTypeName,
                    // Taken here rather than left for whoever follows the edge.
                    // An edge is a claim about the values stored under a field,
                    // and which names those values are keyed by is the schema's
                    // answer to give - a caller working it out again is a second
                    // home for the rule and eventually a different answer.
                    type.Spellings.Of(field)));
            }
        }

        return new ReferenceGraph(schema, edges);
    }

    /// <summary>
    /// The references a record type carries, its inherited ones included.
    /// </summary>
    /// <param name="recordTypeName">The record type to look up.</param>
    /// <returns>Its edges, in a stable order. Empty for a type with none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recordTypeName"/> is null.</exception>
    public IEnumerable<ReferenceEdge> From(string recordTypeName)
    {
        ArgumentNullException.ThrowIfNull(recordTypeName);
        return _bySource[recordTypeName];
    }

    /// <summary>
    /// Whether a record of <paramref name="actualTypeName"/> is a permitted
    /// target of an edge declaring <paramref name="referentTypeName"/>.
    /// </summary>
    /// <param name="referentTypeName">The kind of record the edge permits.</param>
    /// <param name="actualTypeName">The kind of record actually pointed at.</param>
    /// <returns>True where the actual kind is the permitted one or derives from it.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <remarks>
    /// A field permitting one kind of record permits the kinds that derive from
    /// it, because a record of a derived kind carries everything whoever reads
    /// the reference expects to find. Comparing the two names outright would
    /// report every such reference in the game as pointing at the wrong kind of
    /// record.
    /// </remarks>
    public bool Permits(string referentTypeName, string actualTypeName)
    {
        ArgumentNullException.ThrowIfNull(referentTypeName);
        ArgumentNullException.ThrowIfNull(actualTypeName);

        var walked = new HashSet<string>(StringComparer.Ordinal);

        for (var current = actualTypeName; current is not null && walked.Add(current);)
        {
            if (string.Equals(current, referentTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            current = _parentOf.GetValueOrDefault(current);
        }

        return false;
    }

    /// <summary>
    /// Every referent this graph names that is not a record type the schema
    /// knows, in a stable order.
    /// </summary>
    /// <returns>The unresolved referent names. Expected to be empty.</returns>
    /// <remarks>
    /// An edge pointing at a kind of record the schema has never heard of is a
    /// claim nothing can check. Reported by name rather than counted, because
    /// one such name is a bug in the derivation and a hundred is a mismatched
    /// pair of inputs, and the count alone does not tell those apart.
    /// </remarks>
    public IReadOnlyList<string> UnresolvedReferentTypeNames() => ReferentTypeNames
        .Where(name => _schema.Find(name) is null)
        .ToArray();

    // How many sequences deep a reference sits, or -1 where the stored type is
    // not a reference at all. The depth is what tells a field holding one
    // reference from a field holding a list of them, which is a difference a
    // caller reading the values has to know about.
    private static int SequenceDepthOfReference(string storageType)
    {
        const string identifierStorageType = "TweakDBID";
        const string sequencePrefix = "array:";

        var depth = 0;
        var element = storageType;

        while (element.StartsWith(sequencePrefix, StringComparison.Ordinal))
        {
            element = element[sequencePrefix.Length..];
            depth++;
        }

        return string.Equals(element, identifierStorageType, StringComparison.Ordinal) ? depth : -1;
    }
}

/// <summary>
/// One field that holds a reference to another record.
/// </summary>
/// <param name="RecordTypeName">The record type carrying the field.</param>
/// <param name="FieldName">The field.</param>
/// <param name="ReferentTypeName">
/// The kind of record the field may point at, or null where the schema does not
/// know - which is every edge of a schema read from a compiled type model.
/// </param>
/// <param name="IsSequence">
/// Whether the field holds a list of references rather than one.
/// </param>
/// <param name="DeclaringTypeName">
/// The type that declares the field, which may be an ancestor of
/// <paramref name="RecordTypeName"/>.
/// </param>
/// <param name="CandidateFieldNames">
/// Every name the field's stored values might be keyed by, the one the schema
/// keys it by first, as <see cref="FieldSpellings"/> gives them. Following the
/// edge under <paramref name="FieldName"/> alone examines one of these and
/// reports on all of them.
/// </param>
public sealed record ReferenceEdge(
    string RecordTypeName,
    string FieldName,
    string? ReferentTypeName,
    bool IsSequence,
    string DeclaringTypeName,
    IReadOnlyList<string> CandidateFieldNames);
