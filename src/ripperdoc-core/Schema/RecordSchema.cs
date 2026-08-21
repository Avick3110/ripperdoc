namespace Ripperdoc.Core.Schema;

/// <summary>
/// The derived record schema: which record types exist, and the full field set
/// each one resolves to once inheritance is taken into account.
/// </summary>
/// <remarks>
/// Built by <see cref="RecordSchemaDerivation"/> and immutable afterwards.
/// Chain resolution happens once, at construction, so that every consumer sees
/// the same field set for a type and no consumer has to remember to walk a
/// chain.
/// </remarks>
public sealed class RecordSchema
{
    private readonly IReadOnlyDictionary<string, RecordType> _types;

    internal RecordSchema(
        IReadOnlyDictionary<string, RecordType> types,
        IReadOnlyList<DerivationFailure> failures,
        string sourceDescription)
    {
        _types = types;
        Failures = failures;
        SourceDescription = sourceDescription;
        RecordTypeNames = types.Values
            .Where(type => type.IsRecordType)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>What the type information was read from.</summary>
    public string SourceDescription { get; }

    /// <summary>
    /// The names of every record type, in a stable order.
    /// </summary>
    public IReadOnlyList<string> RecordTypeNames { get; }

    /// <summary>
    /// Everything the source or the transform could not derive.
    /// </summary>
    /// <remarks>
    /// Expected to be empty. It is a list rather than a flag because a schema
    /// that is 99% derived is still usable, and saying which 1% is missing is
    /// more useful than refusing to produce anything.
    /// </remarks>
    public IReadOnlyList<DerivationFailure> Failures { get; }

    /// <summary>
    /// How many fields the record types declare themselves, added up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Counts declarations, not resolved slots: a field declared once on a
    /// record type and inherited by fifty others is one declared field and
    /// fifty-one resolved slots. Both numbers are worth having and they answer
    /// different questions.
    /// </para>
    /// <para>
    /// Declarations on ancestors that are not themselves record types are not
    /// counted here, though they do appear in
    /// <see cref="ResolvedFieldSlotCount"/> and in every record type that
    /// inherits them. That is deliberate - this number describes the record
    /// surface - but it means the two counts can move independently, and a
    /// type model that moved a field onto such an ancestor would lower this one
    /// without losing anything.
    /// </para>
    /// </remarks>
    public int DeclaredFieldCount => _types.Values
        .Where(type => type.IsRecordType)
        .Sum(type => type.DeclaredFields.Count);

    /// <summary>
    /// How many fields all record types resolve to once inheritance is applied,
    /// added up.
    /// </summary>
    public int ResolvedFieldSlotCount => _types.Values
        .Where(type => type.IsRecordType)
        .Sum(type => type.Fields.Count);

    /// <summary>
    /// The record type of that name, or null if the schema does not know it.
    /// </summary>
    /// <param name="typeName">The type name to look up.</param>
    /// <returns>The type, or null.</returns>
    /// <remarks>
    /// Null rather than an empty type: a caller that meets a type this schema
    /// has never heard of needs to be able to tell that apart from a type with
    /// no fields, and an empty field set would silently merge the two.
    /// </remarks>
    public RecordType? Find(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        return _types.TryGetValue(typeName, out var type) && type.IsRecordType ? type : null;
    }

    /// <summary>
    /// Every type in the schema, including ancestors that are not themselves
    /// record types.
    /// </summary>
    /// <returns>The types, in a stable order.</returns>
    public IReadOnlyList<RecordType> AllTypes() => _types.Values
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToArray();
}

/// <summary>
/// One type in the schema, with its declared and its resolved field sets.
/// </summary>
public sealed class RecordType
{
    internal RecordType(
        string name,
        string? baseTypeName,
        bool isRecordType,
        IReadOnlyDictionary<string, RecordField> declaredFields,
        IReadOnlyDictionary<string, RecordField> fields)
    {
        Name = name;
        BaseTypeName = baseTypeName;
        IsRecordType = isRecordType;
        DeclaredFields = declaredFields;
        Fields = fields;
    }

    /// <summary>The type's name.</summary>
    public string Name { get; }

    /// <summary>The type it inherits from, or null where the chain ends.</summary>
    public string? BaseTypeName { get; }

    /// <summary>
    /// Whether this is a record type, as opposed to an ancestor carried so that
    /// a record type's chain resolves.
    /// </summary>
    public bool IsRecordType { get; }

    /// <summary>The fields this type declares itself, keyed by field name.</summary>
    public IReadOnlyDictionary<string, RecordField> DeclaredFields { get; }

    /// <summary>
    /// Every field a value of this type can carry, inherited ones included,
    /// keyed by field name.
    /// </summary>
    public IReadOnlyDictionary<string, RecordField> Fields { get; }
}

/// <summary>
/// One field in the schema.
/// </summary>
/// <param name="Name">
/// The field's name as stored values are keyed by it.
/// </param>
/// <param name="StorageType">The name of the type the value is stored as.</param>
/// <param name="DeclaringTypeName">
/// The type that declares this field, which is not necessarily the type it was
/// resolved for.
/// </param>
public sealed record RecordField(string Name, string StorageType, string DeclaringTypeName);
