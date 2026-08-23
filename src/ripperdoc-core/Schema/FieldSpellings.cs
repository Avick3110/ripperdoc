namespace Ripperdoc.Core.Schema;

/// <summary>
/// Which names a record type's stored values can be keyed by, and which field
/// each of those names belongs to.
/// </summary>
/// <remarks>
/// <para>
/// A schema derived from accessor shapes knows a field's name only up to its
/// capitalisation, so it offers more than one spelling and the data decides
/// between them. That turns "the identifier for this field on this record" into
/// a question with several answers, and every caller that asks it has to ask it
/// the same way - probe all the spellings, and leave alone any spelling that is
/// really some other field's name.
/// </para>
/// <para>
/// This is the one place that rule lives. A caller that reaches past it and
/// takes a field's name straight to the identifier arithmetic gets one probe out
/// of several, finds nothing, and reports having checked - which is a clean
/// result over a check that never happened, and is worse than a red one. Going
/// through here is the default; a bare name is the exception, and one that
/// should say why it is not asking for the alternatives.
/// </para>
/// <para>
/// Both questions are answered from one construction because they are one rule
/// read in two directions. Two homes for it will disagree eventually: the
/// forward direction offering a spelling the reverse direction resolves to a
/// different field is exactly the disagreement that is invisible until a field
/// is condemned on another field's values.
/// </para>
/// </remarks>
public sealed class FieldSpellings
{
    private readonly Dictionary<string, IReadOnlyList<string>> _candidates;
    private readonly Dictionary<string, RecordField> _byName;

    internal FieldSpellings(IReadOnlyDictionary<string, RecordField> fields)
    {
        _candidates = new Dictionary<string, IReadOnlyList<string>>(fields.Count, StringComparer.Ordinal);
        _byName = new Dictionary<string, RecordField>(fields.Count, StringComparer.Ordinal);

        foreach (var field in fields.Values)
        {
            _byName[field.Name] = field;
        }

        foreach (var field in fields.Values)
        {
            var candidates = new List<string>(1 + field.AlternateNames.Count) { field.Name };

            foreach (var alternate in field.AlternateNames)
            {
                // A spelling that is already some other field's own name is not
                // a spelling of this one. That field is the better answer, and
                // probing under it here would take the other field's stored
                // values as evidence about this one - agreeing with them, or
                // being condemned by them, on a name this field was only
                // guessing at.
                if (_byName.TryGetValue(alternate, out var owner) && !ReferenceEquals(owner, field))
                {
                    continue;
                }

                if (!candidates.Contains(alternate, StringComparer.Ordinal))
                {
                    candidates.Add(alternate);
                }
            }

            _candidates[field.Name] = candidates;

            foreach (var alternate in candidates.Skip(1))
            {
                _byName.TryAdd(alternate, field);
            }
        }
    }

    /// <summary>
    /// Every name a value of this field might be stored under, the one the
    /// schema keys it by first.
    /// </summary>
    /// <param name="field">The field to spell.</param>
    /// <returns>
    /// The names, in probe order. Always at least the field's own name; more
    /// only where the source could not recover which spelling stored values
    /// use.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is null.</exception>
    /// <remarks>
    /// A field this set was not built from gets its own name back and nothing
    /// else. That is what the schema knows about a field it does not carry, and
    /// inventing alternatives for it would be guessing on behalf of a caller
    /// who is already off the map.
    /// </remarks>
    public IReadOnlyList<string> Of(RecordField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return _candidates.TryGetValue(field.Name, out var candidates) ? candidates : new[] { field.Name };
    }

    /// <summary>
    /// The field a stored name belongs to, under any spelling the schema offers.
    /// </summary>
    /// <param name="name">The name to resolve.</param>
    /// <returns>The field, or null where this type has none of that name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public RecordField? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _byName.GetValueOrDefault(name);
    }
}
