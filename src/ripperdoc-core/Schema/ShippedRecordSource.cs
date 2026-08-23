namespace Ripperdoc.Core.Schema;

/// <summary>
/// Real shipped values, as the thing that arbitrates a schema claim.
/// </summary>
/// <remarks>
/// <para>
/// A schema derived from a type model says which fields ought to exist. Only
/// the shipped database says which ones actually carry values, and it is
/// keyed by an identifier that is pure arithmetic over a name - so the schema
/// can be checked against it exhaustively, without any name table, and without
/// anything generated from a game install.
/// </para>
/// <para>
/// This interface is what keeps that check testable. The real implementation
/// reads a database a user owns and this project may not redistribute; a test
/// implementation constructs a handful of values in memory. The manifest
/// cannot tell the difference, which is what lets its logic be covered on a
/// machine with no game on it.
/// </para>
/// </remarks>
public interface IShippedRecordSource
{
    /// <summary>
    /// What was read, in words fit for a provenance block. Names the database,
    /// never the path it was found at.
    /// </summary>
    string Description { get; }

    /// <summary>How many stored values the database holds in total.</summary>
    int StoredValueCount { get; }

    /// <summary>Every record in the database.</summary>
    IEnumerable<ShippedRecord> Records { get; }

    /// <summary>
    /// The storage type of a stored value, if the database holds one under this
    /// identifier.
    /// </summary>
    /// <param name="identifier">The identifier to look up.</param>
    /// <param name="storageType">
    /// The name of the type the value is stored as, or null if the database
    /// holds the value but cannot say what type it is.
    /// </param>
    /// <returns>True if the database holds a value under this identifier.</returns>
    /// <remarks>
    /// The two failures are deliberately distinguishable: a value that is
    /// absent and a value whose type cannot be read mean different things about
    /// a schema field, and collapsing them would report one as the other.
    /// </remarks>
    bool TryGetStoredValueType(ulong identifier, out string? storageType);
}

/// <summary>
/// One record in a shipped database.
/// </summary>
/// <param name="Identifier">The record's identifier.</param>
/// <param name="TypeName">The name of the record's type.</param>
public readonly record struct ShippedRecord(ulong Identifier, string TypeName);

/// <summary>
/// Stored values read as the record identifiers they name.
/// </summary>
/// <remarks>
/// Separate from <see cref="IShippedRecordSource"/> because arbitrating a
/// schema and following a reference are different questions, and only the
/// second one needs a stored value's contents rather than its type. Keeping
/// them apart means a source built to answer the first is not obliged to
/// answer the second, and neither check is written against a surface wider
/// than it uses.
/// </remarks>
public interface IStoredReferenceSource
{
    /// <summary>
    /// The record identifiers a stored value names.
    /// </summary>
    /// <param name="identifier">The identifier of the value to read.</param>
    /// <param name="targets">
    /// The identifiers it names, in stored order. Empty where the value is a
    /// list with nothing in it.
    /// </param>
    /// <returns>
    /// True where a value is there and is an identifier, or a list of them.
    /// False where there is no value, or where the value is something else.
    /// </returns>
    /// <remarks>
    /// A value of some other type answers false rather than an empty list. The
    /// two would otherwise be indistinguishable, and a caller counting
    /// references would read "this holds no references" off a field whose
    /// contents it never managed to read.
    /// </remarks>
    bool TryGetStoredIdentifiers(ulong identifier, out IReadOnlyList<ulong> targets);
}
