using Ripperdoc.Core.Dump;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// Record type information derived from type information generated on the
/// machine the game is installed on - the mode that costs a generation step and
/// buys what the inherited mode names as lost.
/// </summary>
/// <remarks>
/// <para>
/// The generated type information does not hand over a field set. Record types
/// register no properties at all; what they register is accessors, and the
/// field surface has to be recovered from the shapes of those. That recovery is
/// mechanical over every record type alike - the rules below are about accessor
/// shapes and know nothing about any particular record type, which is what
/// keeps this a derivation rather than a hand-maintained schema wearing a
/// generated one's clothes.
/// </para>
/// <para>
/// Two things this source knows that the inherited mode cannot. It knows what
/// kind of record a reference points at, because the accessor returns the
/// resolved record while the stored value keeps only an identifier. And it
/// knows the field's name only up to its capitalisation, because an accessor
/// name is capitalised by the convention that names accessors rather than by
/// the convention that keys stored values - so both spellings are carried as
/// candidates and real data decides, which is why this source reports fields
/// with alternates and the reflected one does not.
/// </para>
/// </remarks>
public sealed class DumpRecordTypeSource : IRecordTypeSource
{
    private readonly DumpTypeModel _model;

    /// <summary>
    /// A source over generated type information already read into memory.
    /// </summary>
    /// <param name="model">The type information.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public DumpRecordTypeSource(DumpTypeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <inheritdoc />
    public string Description => _model.Description;

    /// <inheritdoc />
    public RecordTypeSourceReading Read()
    {
        var failures = new List<DerivationFailure>();
        var shapes = new List<RecordTypeShape>();
        var wanted = new HashSet<string>(StringComparer.Ordinal);

        // Ancestors are followed rather than filtered for, so a chain cannot end
        // early because an ancestor is not itself a record type. A field an
        // ancestor declares is a field the record carries.
        //
        // A parent the information names and does not describe stops the walk
        // and is not collected. It cannot be: there is nothing to read a shape
        // from. The chain that reaches it keeps naming it, so the derivation
        // meets the break and states it in the sentence it has for exactly this
        // - which is a better answer than the one collecting it gave, where the
        // pass below asked for a description that was never there and the read
        // ended on the framework's own out-of-range complaint.
        foreach (var name in _model.Classes.Keys.Where(RecordTypeNaming.IsRecordTypeName))
        {
            for (var current = name; current is not null;)
            {
                if (!_model.Classes.TryGetValue(current, out var walked) || !wanted.Add(current))
                {
                    break;
                }

                current = walked.ParentName;
            }
        }

        foreach (var name in wanted.OrderBy(name => name, StringComparer.Ordinal))
        {
            var type = _model.Classes[name];
            var isRecordType = RecordTypeNaming.IsRecordTypeName(type.Name);

            shapes.Add(new RecordTypeShape(
                type.Name,
                type.ParentName,
                isRecordType,
                // Fields are recovered from record types only. An ancestor that
                // is not one is in this reading so that a record type's chain
                // terminates, and the accessors it registers are the ones the
                // engine puts on everything - asking an object its class, its
                // identifier, its text - rather than accessors for stored
                // values. Reading those as fields would put a field on all 965
                // record types that no record anywhere stores a value under.
                isRecordType ? FieldsOf(type, failures) : Array.Empty<RecordFieldShape>()));
        }

        foreach (var key in _model.UnrecognisedKeys)
        {
            failures.Add(new DerivationFailure(
                key,
                null,
                "The generated type information carries this key and this reader does not read it, so "
                + "whatever it says is not in this schema."));
        }

        // What the model itself could not take in. A type the model does not
        // describe is one this schema cannot carry either, and a schema that
        // was short by a type without saying so is short in a way nothing
        // downstream can notice.
        foreach (var failure in _model.ReadFailures)
        {
            failures.Add(new DerivationFailure(_model.Description, null, failure));
        }

        return new RecordTypeSourceReading(shapes, failures);
    }

    /// <summary>
    /// Recover a type's declared fields from the shapes of its accessors.
    /// </summary>
    private static IReadOnlyList<RecordFieldShape> FieldsOf(DumpClass type, List<DerivationFailure> failures)
    {
        var byName = type.Functions.ToLookup(function => function.Name, StringComparer.Ordinal);
        var fields = new Dictionary<string, RecordFieldShape>(StringComparer.Ordinal);

        foreach (var function in type.Functions)
        {
            if (IsAccessorHelper(function, byName))
            {
                continue;
            }

            var runtimeType = ValueTypeOf(function);
            if (runtimeType is null)
            {
                continue;
            }

            var storageType = StoredForm(runtimeType);
            if (!StorageTypeName.IsUsable(storageType))
            {
                failures.Add(new DerivationFailure(
                    type.Name,
                    function.Name,
                    $"This accessor's '{runtimeType}' gives '{storageType}', which names no storage type, so "
                    + "this field is not in this schema."));
                continue;
            }

            var stored = StoredName(function.Name);
            var field = new RecordFieldShape(
                stored,
                storageType,
                // The accessor's own spelling is the other candidate. A stored
                // value really is keyed either way in shipped data, and the
                // schema cannot tell which from the accessor alone.
                string.Equals(stored, function.Name, StringComparison.Ordinal)
                    ? Array.Empty<string>()
                    : new[] { function.Name },
                ReferentOf(runtimeType));

            if (fields.TryAdd(stored, field))
            {
                continue;
            }

            failures.Add(new DerivationFailure(
                type.Name,
                stored,
                $"Two accessors give this field name, as '{fields[stored].StorageType}' and as "
                + $"'{storageType}'; the first was kept."));
        }

        return fields.Values.OrderBy(field => field.FieldName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Whether an accessor is one of the several the runtime registers around a
    /// field rather than the one that names it.
    /// </summary>
    /// <remarks>
    /// A field with an array value gets a count accessor and an item accessor
    /// beside the accessor for the array itself, and a field holding a reference
    /// gets a second accessor returning the same value in another form. Each of
    /// those names a field that another accessor already names, so treating them
    /// as fields would invent field names the game does not key anything under.
    /// </remarks>
    /// <remarks>
    /// Every rule here matches on shape as well as on name. A rule that matched
    /// a name alone would eventually meet a genuine field whose name ends the
    /// same way and drop it - and a field dropped here is one the schema never
    /// mentions again, which is the quiet kind of wrong answer rather than the
    /// loud one.
    /// </remarks>
    private static bool IsAccessorHelper(DumpFunction function, ILookup<string, DumpFunction> byName)
    {
        var name = function.Name;

        if (name.StartsWith(CountAndItemPrefix, StringComparison.Ordinal))
        {
            if (name.EndsWith(CountSuffix, StringComparison.Ordinal)
                && function.Parameters.Count == 0
                && function.ReturnTypeName == IndexTypeName)
            {
                return true;
            }

            if ((name.EndsWith(ItemSuffix, StringComparison.Ordinal)
                    || name.EndsWith(ItemReferenceSuffix, StringComparison.Ordinal))
                && function.Parameters.Count == 1
                && function.Parameters[0].TypeName == IndexTypeName)
            {
                return true;
            }
        }

        // A membership test is not named here. It takes the value to look for as
        // an ordinary parameter, and an accessor that reads a parameter gives no
        // value of its own - so it is already not a field, by the rule that
        // decides what an accessor gives rather than by its name.
        //
        // Naming it here as well cost something. The only shape a name-and-count
        // test could reach that the value rule does not is an accessor that
        // writes into an output parameter, which is a genuine field: a field
        // whose name ends this way would have been dropped, and the two arms
        // above avoid that by matching on the parameter's type as well as the
        // name.

        // The second form of a reference accessor, recognised by the accessor it
        // is a second form of actually being there. Recognising it by its name
        // alone would drop a genuine field that happens to end this way.
        return name.EndsWith(ReferenceSuffix, StringComparison.Ordinal)
            && byName[name[..^ReferenceSuffix.Length]].Any(other => other.Parameters.Count == 0);
    }

    /// <summary>
    /// The type of the value an accessor gives, or null where it gives none.
    /// </summary>
    private static string? ValueTypeOf(DumpFunction function)
    {
        if (function.Parameters.Count == 0)
        {
            // An accessor that takes nothing and returns nothing gives no
            // value, and is a method the runtime registers rather than a field.
            // Read as a field it would be one of storage type "None" - a name
            // that passes for a storage type, so nothing downstream refuses it,
            // and no stored value is ever keyed by it.
            return function.ReturnTypeName is null or NoValueTypeName ? null : function.ReturnTypeName;
        }

        // An accessor that returns nothing and writes into a parameter the
        // generated information marks as an output is giving that parameter's
        // value. The marker is the generated information's own statement about
        // the parameter rather than an inference from the shape around it.
        if (function.Parameters.Count == 1
            && function.Parameters[0].IsOutput
            && function.ReturnTypeName is null or NoValueTypeName)
        {
            return function.Parameters[0].TypeName;
        }

        return null;
    }

    /// <summary>
    /// The type a value of this runtime type is stored as.
    /// </summary>
    /// <remarks>
    /// An accessor answers in the type the running game hands you, which for
    /// two families is not the type the value is stored as. A reference is
    /// stored as an identifier and resolved into the record on the way out, and
    /// a resource reference is stored as a path to the resource. Both are
    /// rewritten to what is on disk, because a schema whose types describe the
    /// resolved form would disagree with every stored value it claims.
    /// </remarks>
    private static string StoredForm(string runtimeType)
    {
        if (runtimeType.StartsWith(SequencePrefix, StringComparison.Ordinal))
        {
            return SequencePrefix + StoredForm(runtimeType[SequencePrefix.Length..]);
        }

        if (IsReference(runtimeType))
        {
            return IdentifierTypeName;
        }

        return string.Equals(runtimeType, ResourceTokenTypeName, StringComparison.Ordinal)
            ? ResourceReferenceTypeName
            : runtimeType;
    }

    /// <summary>
    /// The kind of record a reference points at, or null where the value is not
    /// a reference.
    /// </summary>
    private static string? ReferentOf(string runtimeType)
    {
        var element = runtimeType;
        while (element.StartsWith(SequencePrefix, StringComparison.Ordinal))
        {
            element = element[SequencePrefix.Length..];
        }

        if (!IsReference(element))
        {
            return null;
        }

        var referent = element[(element.IndexOf(':') + 1)..];
        return referent.Length == 0 ? null : referent;
    }

    private static bool IsReference(string runtimeType) =>
        runtimeType.StartsWith(ReferencePrefix, StringComparison.Ordinal)
        || runtimeType.StartsWith(WeakReferencePrefix, StringComparison.Ordinal);

    /// <summary>
    /// The spelling a stored value is most likely keyed by, from the accessor's
    /// own name.
    /// </summary>
    /// <remarks>
    /// Accessors are named with a leading capital and stored values are
    /// overwhelmingly keyed without one, so the leading capital comes off. A run
    /// of capitals is an initialism and stays together except for its last
    /// letter, which begins the next word: the alternative would lower a whole
    /// initialism and produce a name no stored value is keyed by. This is the
    /// likelier of the two spellings and never the certain one, which is why the
    /// other is carried as a candidate rather than discarded here.
    /// </remarks>
    private static string StoredName(string accessorName)
    {
        if (accessorName.Length == 0)
        {
            return accessorName;
        }

        var capitals = 0;
        while (capitals < accessorName.Length && char.IsUpper(accessorName[capitals]))
        {
            capitals++;
        }

        if (capitals <= 1)
        {
            return char.ToLowerInvariant(accessorName[0]) + accessorName[1..];
        }

        if (capitals < accessorName.Length)
        {
            capitals--;
        }

        return accessorName[..capitals].ToLowerInvariant() + accessorName[capitals..];
    }

    private const string SequencePrefix = "array:";
    private const string ReferencePrefix = "handle:";
    private const string WeakReferencePrefix = "whandle:";
    private const string IdentifierTypeName = "TweakDBID";
    private const string ResourceTokenTypeName = "redResourceReferenceScriptToken";
    private const string ResourceReferenceTypeName = "raRef:CResource";
    private const string IndexTypeName = "Int32";
    private const string NoValueTypeName = "None";
    private const string CountAndItemPrefix = "Get";
    private const string CountSuffix = "Count";
    private const string ItemSuffix = "Item";
    private const string ItemReferenceSuffix = "ItemHandle";
    private const string ReferenceSuffix = "Handle";
}
