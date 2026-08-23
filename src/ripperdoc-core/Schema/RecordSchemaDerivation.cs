namespace Ripperdoc.Core.Schema;

/// <summary>
/// The derivation transform: type shapes in, resolved record schema out.
/// </summary>
/// <remarks>
/// <para>
/// This is the step the coverage-by-construction rule is really about. It is
/// mechanical over whatever the source hands it and it knows nothing about any
/// particular record type - there is no table of special cases here, and a
/// per-record-type branch appearing in this file would mean the schema had
/// started being hand-maintained one exception at a time.
/// </para>
/// <para>
/// What it actually does is resolve inheritance and refuse to lose anything on
/// the way. A record type's usable field set is its own declarations plus
/// every ancestor's, with the nearest declaration winning where a name repeats.
/// Anything that cannot be resolved - a base type the reading does not contain,
/// a cycle, a field with no name - becomes a stated failure rather than a
/// quietly shorter field set.
/// </para>
/// </remarks>
public static class RecordSchemaDerivation
{
    /// <summary>
    /// Derive the record schema from a source.
    /// </summary>
    /// <param name="source">The type information to derive from.</param>
    /// <returns>The schema, carrying whatever could not be derived.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static RecordSchema Derive(IRecordTypeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var reading = source.Read();
        return Derive(reading, source.Description);
    }

    /// <summary>
    /// Derive the record schema from a reading a source has already produced.
    /// </summary>
    /// <param name="reading">The type shapes and the source's own failures.</param>
    /// <param name="sourceDescription">What the reading came from.</param>
    /// <returns>The schema, carrying whatever could not be derived.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static RecordSchema Derive(RecordTypeSourceReading reading, string sourceDescription)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(sourceDescription);

        var failures = new List<DerivationFailure>(reading.Failures);

        var shapes = new Dictionary<string, RecordTypeShape>(StringComparer.Ordinal);
        foreach (var shape in reading.Types.OrderBy(type => type.TypeName, StringComparer.Ordinal))
        {
            if (shapes.TryAdd(shape.TypeName, shape))
            {
                continue;
            }

            failures.Add(new DerivationFailure(
                shape.TypeName,
                null,
                "The reading declares this type more than once; the first declaration was kept."));
        }

        var declared = new Dictionary<string, Dictionary<string, RecordField>>(StringComparer.Ordinal);
        foreach (var (typeName, shape) in shapes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            declared[typeName] = DeclaredFieldsOf(typeName, shape, failures);
        }

        var types = new Dictionary<string, RecordType>(StringComparer.Ordinal);
        foreach (var (typeName, shape) in shapes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var fields = Resolve(typeName, shapes, declared, failures);
            types[typeName] = new RecordType(
                typeName,
                shape.BaseTypeName,
                shape.IsRecordType,
                declared[typeName],
                fields);
        }

        return new RecordSchema(types, Ordered(failures), sourceDescription);
    }

    private static Dictionary<string, RecordField> DeclaredFieldsOf(
        string typeName,
        RecordTypeShape shape,
        List<DerivationFailure> failures)
    {
        var fields = new Dictionary<string, RecordField>(StringComparer.Ordinal);

        foreach (var field in shape.DeclaredFields)
        {
            if (string.IsNullOrEmpty(field.FieldName))
            {
                failures.Add(new DerivationFailure(
                    typeName,
                    null,
                    $"A field of storage type '{field.StorageType}' has no name, so no stored value can be addressed through it."));
                continue;
            }

            var resolved = new RecordField(
                field.FieldName,
                field.StorageType,
                typeName,
                field.AlternateFieldNames,
                field.ReferentTypeName);

            if (fields.TryAdd(field.FieldName, resolved))
            {
                continue;
            }

            failures.Add(new DerivationFailure(
                typeName,
                field.FieldName,
                $"Declared more than once on this type, as '{fields[field.FieldName].StorageType}' and as "
                + $"'{field.StorageType}'; the first declaration was kept."));
        }

        return fields;
    }

    // Every type in the reading is resolved exactly once, from itself outward,
    // so there is nothing to remember between calls. A memo here would be a
    // lookup that never hit, and one that reads as memoisation to whoever
    // touches this next.
    private static IReadOnlyDictionary<string, RecordField> Resolve(
        string typeName,
        IReadOnlyDictionary<string, RecordTypeShape> shapes,
        IReadOnlyDictionary<string, Dictionary<string, RecordField>> declared,
        List<DerivationFailure> failures)
    {
        var fields = new Dictionary<string, RecordField>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = typeName;

        while (current is not null)
        {
            if (!visited.Add(current))
            {
                failures.Add(new DerivationFailure(
                    typeName,
                    null,
                    $"The inheritance chain returns to '{current}', so it does not terminate; "
                    + "resolution stopped there and the fields beyond it are not in this schema."));
                break;
            }

            if (!shapes.TryGetValue(current, out var shape))
            {
                failures.Add(new DerivationFailure(
                    typeName,
                    null,
                    $"The inheritance chain reaches '{current}', which the reading does not contain, "
                    + "so any fields it declares are not in this schema."));
                break;
            }

            // Nearest declaration wins: the walk runs from the type outward, so
            // the first name seen is the one closest to it.
            foreach (var field in declared[current].Values)
            {
                fields.TryAdd(field.Name, field);
            }

            current = shape.BaseTypeName;
        }

        return fields;
    }

    private static IReadOnlyList<DerivationFailure> Ordered(IEnumerable<DerivationFailure> failures) => failures
        .OrderBy(failure => failure.TypeName, StringComparer.Ordinal)
        .ThenBy(failure => failure.MemberName ?? string.Empty, StringComparer.Ordinal)
        .ThenBy(failure => failure.Reason, StringComparer.Ordinal)
        .ToArray();
}
