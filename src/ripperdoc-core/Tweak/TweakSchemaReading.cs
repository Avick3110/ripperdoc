using Ripperdoc.Core.Schema;

namespace Ripperdoc.Core.Tweak;

/// <summary>
/// A tweak layer read against the record schema: which types it names, and
/// which properties it sets that the schema does not have.
/// </summary>
/// <remarks>
/// <para>
/// A property the schema lacks is not a curiosity. The framework refuses such a
/// write - it logs the property as unknown and moves on - so a mod naming one
/// has written a line that does nothing, and nothing on the user's machine
/// tells either of them. It is also the measurement that says whether the
/// schema is complete enough to arbitrate real mods rather than only shipped
/// data.
/// </para>
/// <para>
/// Only records whose type this reading can resolve are checked. A record the
/// layer edits without declaring has its type in the shipped database, which a
/// run without one cannot consult - so those are counted as unresolved and
/// named, never quietly treated as having no unknown properties.
/// </para>
/// </remarks>
public sealed class TweakSchemaReading
{
    private TweakSchemaReading(
        bool unknownPropertiesWereCounted,
        string propertySourceDescription,
        int recordsWithAResolvedType,
        IReadOnlyList<string> recordsWithNoResolvedType,
        IReadOnlyList<UnknownRecordType> typesTheSchemaLacks,
        int propertiesChecked,
        IReadOnlyList<UnknownProperty> propertiesTheSchemaLacks)
    {
        UnknownPropertiesWereCounted = unknownPropertiesWereCounted;
        PropertySourceDescription = propertySourceDescription;
        RecordsWithAResolvedType = recordsWithAResolvedType;
        RecordsWithNoResolvedType = recordsWithNoResolvedType;
        TypesTheSchemaLacks = typesTheSchemaLacks;
        PropertiesChecked = propertiesChecked;
        PropertiesTheSchemaLacks = propertiesTheSchemaLacks;
    }

    /// <summary>
    /// Whether the unknown-property count means anything.
    /// </summary>
    /// <remarks>
    /// False where no framework metadata was supplied. The framework adds
    /// properties the type model does not declare, and they land on the record
    /// types mods edit most - so a count taken without them is not a smaller
    /// answer to the same question, it is a wrong one. Reported as uncounted
    /// rather than as a number nobody should act on.
    /// </remarks>
    public bool UnknownPropertiesWereCounted { get; }

    /// <summary>What the property set was resolved against.</summary>
    public string PropertySourceDescription { get; }

    /// <summary>How many records the layer declares whose type the schema knows.</summary>
    public int RecordsWithAResolvedType { get; }

    /// <summary>
    /// Records the layer writes to whose type this reading could not work out,
    /// in a stable order.
    /// </summary>
    public IReadOnlyList<string> RecordsWithNoResolvedType { get; }

    /// <summary>
    /// Type names the layer declares that the schema does not contain, in a
    /// stable order.
    /// </summary>
    /// <remarks>
    /// A name in canonical form is not thereby a real type. These are reported
    /// as names that did not resolve, which is what they are.
    /// </remarks>
    public IReadOnlyList<UnknownRecordType> TypesTheSchemaLacks { get; }

    /// <summary>How many property writes were checked against the schema.</summary>
    public int PropertiesChecked { get; }

    /// <summary>
    /// Property writes naming something neither the type model nor the
    /// framework's own metadata has, in a stable order.
    /// </summary>
    /// <remarks>
    /// Empty, and meaningless, where
    /// <see cref="UnknownPropertiesWereCounted"/> is false.
    /// </remarks>
    public IReadOnlyList<UnknownProperty> PropertiesTheSchemaLacks { get; }

    /// <summary>
    /// Read a layer's declarations and writes against a schema.
    /// </summary>
    /// <param name="documents">The layer's files as read, in read order.</param>
    /// <param name="schema">The record schema to resolve against.</param>
    /// <param name="extraFlats">
    /// The properties the framework adds on top of the schema's.
    /// <see cref="TweakExtraFlats.None"/> is accepted and turns the
    /// unknown-property count off rather than making it wrong.
    /// </param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TweakSchemaReading Of(
        IReadOnlyList<TweakDocument> documents,
        RecordSchema schema,
        TweakExtraFlats extraFlats)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(extraFlats);

        // Types first, over the whole layer, because a record cloned from one
        // the layer declares later still takes that type - the framework
        // resolves the whole changeset before it commits any of it, so a
        // one-pass read in file order would resolve fewer types than the game
        // does and under-report what could be checked.
        var declaredTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        var clonedFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        var unknownTypes = new List<UnknownRecordType>();

        foreach (var declaration in documents.SelectMany(document => document.Declarations))
        {
            // A record declared twice keeps the first declaration.
            if (declaredTypes.ContainsKey(declaration.RecordName)
                || clonedFrom.ContainsKey(declaration.RecordName))
            {
                continue;
            }

            if (declaration.DeclaredTypeName is { } spelled)
            {
                var canonical = RecordTypeNaming.Canonicalise(spelled);
                declaredTypes[declaration.RecordName] = canonical;

                if (schema.Find(canonical) is null)
                {
                    unknownTypes.Add(new UnknownRecordType(declaration.RecordName, spelled, canonical));
                }
            }
            else if (declaration.BaseName is { } baseName)
            {
                clonedFrom[declaration.RecordName] = baseName;
            }
        }

        var propertiesChecked = 0;
        var unknownProperties = new List<UnknownProperty>();
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (document, write) in documents
                     .SelectMany(document => document.Writes.Select(write => (document, write))))
        {
            if (write.OwningRecordName is not { } recordName)
            {
                continue;
            }

            var typeName = ResolveType(recordName, declaredTypes, clonedFrom);
            if (typeName is null || schema.Find(typeName) is not { } type)
            {
                unresolved.Add(recordName);
                continue;
            }

            propertiesChecked++;

            var property = write.FlatName[(recordName.Length + 1)..];
            if (extraFlats.IsPresent
                && !type.Fields.ContainsKey(property)
                && !extraFlats.Declares(typeName, property))
            {
                unknownProperties.Add(new UnknownProperty(
                    document.RelativePath,
                    write.Line,
                    recordName,
                    typeName,
                    property));
            }
        }

        return new TweakSchemaReading(
            extraFlats.IsPresent,
            $"{schema.SourceDescription}, plus {extraFlats.Description}",
            declaredTypes.Count + clonedFrom.Count,
            unresolved.ToArray(),
            unknownTypes,
            propertiesChecked,
            unknownProperties);
    }

    // A clone takes its source's type, and a source can itself be a clone. The
    // chain is followed with a limit rather than trusted to terminate: a layer
    // whose declarations form a cycle would otherwise hang the read, and the
    // framework refuses such a record rather than resolving it.
    private static string? ResolveType(
        string recordName,
        IReadOnlyDictionary<string, string> declaredTypes,
        IReadOnlyDictionary<string, string> clonedFrom)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = recordName;

        while (seen.Add(current))
        {
            if (declaredTypes.TryGetValue(current, out var typeName))
            {
                return typeName;
            }

            if (!clonedFrom.TryGetValue(current, out var baseName))
            {
                return null;
            }

            current = baseName;
        }

        return null;
    }
}

/// <summary>
/// A record type the layer declares that the schema does not contain.
/// </summary>
/// <param name="RecordName">The record declared with it.</param>
/// <param name="SpelledAs">The name as the file wrote it.</param>
/// <param name="Canonical">The canonical spelling that failed to resolve.</param>
public sealed record UnknownRecordType(string RecordName, string SpelledAs, string Canonical);

/// <summary>
/// A property write naming something the record's type does not have.
/// </summary>
/// <param name="RelativePath">The file that wrote it.</param>
/// <param name="Line">The line in that file.</param>
/// <param name="RecordName">The record written to.</param>
/// <param name="TypeName">The record's type, canonically spelled.</param>
/// <param name="PropertyName">The property the schema does not have.</param>
public sealed record UnknownProperty(
    string RelativePath,
    int Line,
    string RecordName,
    string TypeName,
    string PropertyName);
