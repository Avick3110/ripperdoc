using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Records and the references between them, built in memory for one check.
/// </summary>
/// <remarks>
/// The identifiers here are invented and the arithmetic that turns a record and
/// a field into one is the engine's own, so a reference placed by
/// <see cref="PointingFrom"/> is found by exactly the route a real one would
/// be. Nothing of the game's is involved, which is what puts these checks on a
/// bare runner.
/// </remarks>
public sealed class SyntheticReferenceSource : IShippedRecordSource, IStoredReferenceSource
{
    private readonly List<ShippedRecord> _records = [];
    private readonly Dictionary<ulong, IReadOnlyList<ulong>> _references = [];
    private readonly HashSet<ulong> _otherValues = [];

    /// <inheritdoc />
    public string Description => "records constructed for this test";

    /// <inheritdoc />
    public int StoredValueCount => _references.Count + _otherValues.Count;

    /// <inheritdoc />
    public IEnumerable<ShippedRecord> Records => _records;

    /// <summary>Add a record of a given kind.</summary>
    /// <param name="identifier">The record's identifier.</param>
    /// <param name="typeName">Its type.</param>
    /// <returns>This source.</returns>
    public SyntheticReferenceSource WithRecord(ulong identifier, string typeName)
    {
        _records.Add(new ShippedRecord(identifier, typeName));
        return this;
    }

    /// <summary>Store a reference in a record's field.</summary>
    /// <param name="record">The record holding the reference.</param>
    /// <param name="fieldName">The field it is stored in.</param>
    /// <param name="targets">The identifiers it names.</param>
    /// <returns>This source.</returns>
    public SyntheticReferenceSource PointingFrom(ulong record, string fieldName, params ulong[] targets)
    {
        _references[FlatOf(record, fieldName)] = targets;
        return this;
    }

    /// <summary>
    /// Store a value that is present and is not a reference.
    /// </summary>
    /// <param name="record">The record holding it.</param>
    /// <param name="fieldName">The field it is stored in.</param>
    /// <returns>This source.</returns>
    public SyntheticReferenceSource HoldingSomethingElseAt(ulong record, string fieldName)
    {
        _otherValues.Add(FlatOf(record, fieldName));
        return this;
    }

    /// <inheritdoc />
    public bool TryGetStoredValueType(ulong identifier, out string? storageType)
    {
        storageType = _otherValues.Contains(identifier) ? "CName" : "TweakDBID";
        return _references.ContainsKey(identifier) || _otherValues.Contains(identifier);
    }

    /// <inheritdoc />
    public bool TryGetStoredIdentifiers(ulong identifier, out IReadOnlyList<ulong> targets)
    {
        if (_references.TryGetValue(identifier, out var found))
        {
            targets = found;
            return true;
        }

        targets = Array.Empty<ulong>();
        return false;
    }

    private static ulong FlatOf(ulong record, string fieldName)
    {
        if (!TweakIdentifier.TryForField(record, fieldName, out var identifier, out var reason))
        {
            throw new InvalidOperationException(
                $"'{fieldName}' has no identifier on this record: {TweakIdentifier.Describe(reason)}.");
        }

        return identifier;
    }
}
