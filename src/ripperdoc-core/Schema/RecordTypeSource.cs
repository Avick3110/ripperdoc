namespace Ripperdoc.Core.Schema;

/// <summary>
/// A source of record type information, in the shape the derivation transform
/// consumes.
/// </summary>
/// <remarks>
/// <para>
/// The engine has two possible sources of the same facts, and the difference
/// between them is a mode of the product rather than a difference in the
/// schema they produce. One reads the pinned dependency's own type model; the
/// other reads type information generated from a game install. Both answer the
/// same question - which record types exist, what they inherit from, and what
/// fields they declare - so the transform that turns those answers into a
/// schema is written once, against this interface, and neither source gets to
/// hand-shape the result.
/// </para>
/// <para>
/// A source reports what it could not derive rather than dropping it. A member
/// that yields no field is either a member that is not a field, which the
/// source simply does not emit, or a member the source failed on, which it
/// emits as a failure. There is no third case where something disappears.
/// </para>
/// </remarks>
public interface IRecordTypeSource
{
    /// <summary>
    /// What this source read, in words fit to appear in an artifact's
    /// provenance block. Names the origin, never a machine path.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Read every record type this source knows about.
    /// </summary>
    /// <returns>The type shapes, and everything that could not be derived.</returns>
    RecordTypeSourceReading Read();
}

/// <summary>
/// Everything one read of a <see cref="IRecordTypeSource"/> produced.
/// </summary>
/// <param name="Types">
/// The type shapes. Includes the ancestors of record types even where an
/// ancestor is not itself a record type, so that every inheritance chain in
/// the reading terminates inside the reading.
/// </param>
/// <param name="Failures">Members the source could not turn into fields.</param>
public sealed record RecordTypeSourceReading(
    IReadOnlyList<RecordTypeShape> Types,
    IReadOnlyList<DerivationFailure> Failures);

/// <summary>
/// One type as a source reports it, before any chain resolution.
/// </summary>
/// <param name="TypeName">The type's name.</param>
/// <param name="BaseTypeName">
/// The name of the type it inherits from, or null where the chain ends here.
/// A non-null name that is absent from the same reading is a failure, not a
/// chain that quietly stops early.
/// </param>
/// <param name="IsRecordType">
/// Whether this type is itself a record type. False for an ancestor carried
/// only so that a record type's chain resolves.
/// </param>
/// <param name="DeclaredFields">The fields this type declares itself.</param>
public sealed record RecordTypeShape(
    string TypeName,
    string? BaseTypeName,
    bool IsRecordType,
    IReadOnlyList<RecordFieldShape> DeclaredFields);

/// <summary>
/// One declared field as a source reports it.
/// </summary>
/// <param name="FieldName">
/// The field's name as stored values are keyed by it - which is not always the
/// name the source's own programming language uses for it.
/// </param>
/// <param name="StorageType">The name of the type the field's value is stored as.</param>
/// <param name="AlternateFieldNames">
/// Other spellings the same field's stored values might be keyed by, where the
/// source cannot tell which one is used.
/// </param>
/// <param name="ReferentTypeName">
/// The kind of record this field's stored identifier points at, or null where
/// the source does not say - which includes every field that is not a
/// reference.
/// </param>
/// <remarks>
/// <para>
/// <paramref name="AlternateFieldNames"/> exists because one of the two sources
/// genuinely does not know. A source reading a compiled type model is told the
/// stored name outright; a source deriving fields from accessor shapes recovers
/// a name whose capitalisation the accessor does not preserve, and the shipped
/// data uses both spellings. Carrying the alternatives and letting real data
/// decide is the honest form of that, and it is why a field is one field with
/// several possible names rather than several fields.
/// </para>
/// <para>
/// <paramref name="ReferentTypeName"/> is the one thing the generated mode
/// knows and the inherited mode structurally cannot: a stored reference is an
/// identifier, and an identifier says which record is pointed at but not what
/// kind of record was allowed there.
/// </para>
/// </remarks>
public sealed record RecordFieldShape(
    string FieldName,
    string StorageType,
    IReadOnlyList<string> AlternateFieldNames,
    string? ReferentTypeName)
{
    /// <summary>
    /// A field whose stored name is known and which is not a typed reference.
    /// </summary>
    /// <param name="fieldName">The field's name as stored values are keyed by it.</param>
    /// <param name="storageType">The name of the type the value is stored as.</param>
    public RecordFieldShape(string fieldName, string storageType)
        : this(fieldName, storageType, Array.Empty<string>(), null)
    {
    }

    /// <summary>
    /// Every spelling this field's stored values might be keyed by, the primary
    /// one first.
    /// </summary>
    /// <returns>The candidate names, in probe order.</returns>
    public IEnumerable<string> CandidateFieldNames() => new[] { FieldName }.Concat(AlternateFieldNames);
}

/// <summary>
/// Something a source or the transform could not derive, kept rather than
/// dropped.
/// </summary>
/// <param name="TypeName">The type the failure occurred on.</param>
/// <param name="MemberName">
/// The member the failure occurred on, or null where the failure is about the
/// type as a whole.
/// </param>
/// <param name="Reason">What could not be derived, in a sentence.</param>
/// <remarks>
/// A schema that is quietly missing a field gives confident wrong answers
/// about that field forever. A schema that carries the failure gives a wrong
/// answer nowhere and an explanation here.
/// </remarks>
public sealed record DerivationFailure(string TypeName, string? MemberName, string Reason);
