using System.Reflection;
using WolvenKit.RED4.Types;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// Record type information read by reflecting over a type model that is
/// already compiled - the mode that needs nothing generated from a game
/// install.
/// </summary>
/// <remarks>
/// <para>
/// The pinned dependency carries a type model of the game's records, and this
/// source reads it directly rather than re-deriving it. That is not a reduced
/// substitute for the generated mode: it is the mode with no setup, and what
/// it costs is named in the artifact's provenance rather than left for a user
/// to discover.
/// </para>
/// <para>
/// Field names come from the type model's own annotation, never from the
/// programming-language name of the property carrying them. The two disagree
/// for the overwhelming majority of fields, and it is the annotation that
/// matches how stored values are actually keyed.
/// </para>
/// </remarks>
public sealed class ReflectedRecordTypeSource : IRecordTypeSource
{
    private readonly IReadOnlyList<Type> _types;

    /// <summary>
    /// A source over an explicit set of types.
    /// </summary>
    /// <param name="types">
    /// The types to consider. Record types are selected from these by name;
    /// their ancestors are followed and included whether or not they were
    /// listed.
    /// </param>
    /// <param name="description">
    /// What these types came from, in words fit for a provenance block.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ReflectedRecordTypeSource(IEnumerable<Type> types, string description)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(description);

        _types = types.ToArray();
        Description = description;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>
    /// A source over the pinned dependency's own type model.
    /// </summary>
    /// <returns>The source the no-setup mode reads its schema from.</returns>
    public static ReflectedRecordTypeSource FromPinnedTypeModel()
    {
        var assembly = typeof(RedBaseClass).Assembly;
        var name = assembly.GetName();

        return new ReflectedRecordTypeSource(
            assembly.GetTypes(),
            $"{name.Name} {name.Version?.ToString(3) ?? "unknown version"} type model, reflected");
    }

    /// <inheritdoc />
    public RecordTypeSourceReading Read()
    {
        var failures = new List<DerivationFailure>();
        var shapes = new Dictionary<string, RecordTypeShape>(StringComparer.Ordinal);
        var seen = new Dictionary<string, Type>(StringComparer.Ordinal);

        // A record-named type this source will not read is stated rather than
        // dropped. A consumer asking for one that is absent gets the same null
        // whether the game has no such type or this source declined to read
        // the one it has, and those are different answers to the same
        // question.
        var recordTypes = new List<Type>();
        foreach (var type in _types.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            if (!RecordTypeNaming.IsRecordTypeName(type.Name))
            {
                continue;
            }

            var declined = WhyNotUsableRedClass(type);
            if (declined is null)
            {
                recordTypes.Add(type);
                continue;
            }

            failures.Add(new DerivationFailure(type.Name, null, declined));
        }

        foreach (var recordType in recordTypes)
        {
            Type? child = null;

            // Ancestors are followed rather than looked up, so a chain cannot
            // end early just because an ancestor was not in the type list. A
            // field an ancestor declares is a field the record carries.
            for (Type? type = recordType; type is not null && type != typeof(object); type = type.BaseType)
            {
                if (seen.TryGetValue(type.Name, out var alreadyRead))
                {
                    if (alreadyRead != type)
                    {
                        // A schema addresses types by name, so two types
                        // sharing one name cannot both be in it. The chain is
                        // cut at the type below the clash and that type is told
                        // it now has no base, because leaving it pointing at the
                        // name would hand it the other type's fields - which is
                        // a wrong field set rather than a missing one, and the
                        // whole point of stopping here is to have neither
                        // silently.
                        RecordClash(failures, shapes, child, type, alreadyRead);
                    }

                    break;
                }

                seen[type.Name] = type;
                shapes[type.Name] = ShapeOf(type, failures);
                child = type;
            }
        }

        return new RecordTypeSourceReading(
            shapes.Values.OrderBy(shape => shape.TypeName, StringComparer.Ordinal).ToArray(),
            failures);
    }

    private static void RecordClash(
        List<DerivationFailure> failures,
        Dictionary<string, RecordTypeShape> shapes,
        Type? child,
        Type clashing,
        Type alreadyRead)
    {
        var bothNamed =
            $"'{alreadyRead.FullName}' and '{clashing.FullName}' are different types with the same name "
            + $"'{clashing.Name}', and a schema addresses types by name";

        if (child is null)
        {
            failures.Add(new DerivationFailure(
                clashing.Name,
                null,
                $"{bothNamed}; the first was kept and this one is not in this schema."));
            return;
        }

        shapes[child.Name] = shapes[child.Name] with { BaseTypeName = null };
        failures.Add(new DerivationFailure(
            child.Name,
            null,
            $"{bothNamed}. Its inheritance chain was cut here rather than resolved by arrival order, so "
            + $"nothing that '{clashing.Name}' or anything above it declares is in this schema for this "
            + "type - the whole remainder of the chain is gone, not only the clashing type's own fields."));
    }

    // Null where the type can be read. A sentence where it cannot, because the
    // caller's next question is which of these it was.
    private static string? WhyNotUsableRedClass(Type type)
    {
        if (!typeof(RedBaseClass).IsAssignableFrom(type))
        {
            return "This is named like a record type but is not a record class in the type model, so it "
                + "declares no fields this schema could carry and it is not in this schema.";
        }

        if (type.IsGenericTypeDefinition)
        {
            return "This is a generic type definition, which has no one field set to derive from, so it is "
                + "not in this schema.";
        }

        // Nested counts. A public type nested inside another reports IsPublic
        // as false, so testing that alone drops a type that is on the public
        // surface - and drops it for a reason nobody stated.
        if (!type.IsPublic && !type.IsNestedPublic)
        {
            return "This type is not on the public surface, so it is not in this schema.";
        }

        return null;
    }

    private static RecordTypeShape ShapeOf(Type type, List<DerivationFailure> failures)
    {
        var fields = new List<RecordFieldShape>();

        foreach (var property in type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var annotation = property.GetCustomAttribute<REDAttribute>();
            if (annotation is null)
            {
                continue;
            }

            var flags = annotation.Flags is null ? Flags.Empty : new Flags(annotation.Flags);

            string storageType;
            try
            {
                storageType = RedReflection.GetRedTypeFromCSType(property.PropertyType, flags);
            }
            catch (Exception exception)
            {
                failures.Add(new DerivationFailure(
                    type.Name,
                    property.Name,
                    $"The type model does not resolve '{property.PropertyType.Name}' to a storage type: "
                    + exception.Message));
                continue;
            }

            // The type model answers an unmappable property with an empty
            // storage type rather than by refusing, so the answer is checked
            // as well as taken. A field carried with no storage type would
            // match no stored value, be marked unconfirmed, and read as
            // ordinary residue - a wrong field wearing an innocent label.
            if (!StorageTypeName.IsUsable(storageType))
            {
                failures.Add(new DerivationFailure(
                    type.Name,
                    property.Name,
                    $"The type model resolves '{property.PropertyType.Name}' to '{storageType}', which names "
                    + "no storage type, so this field is not in this schema."));
                continue;
            }

            // An empty annotation name means the type model addresses the field
            // by the property's own name. That is the model's rule, not a guess
            // made here.
            var fieldName = string.IsNullOrEmpty(annotation.Name) ? property.Name : annotation.Name;
            fields.Add(new RecordFieldShape(fieldName, storageType));
        }

        var baseType = type.BaseType;
        var baseTypeName = baseType is null || baseType == typeof(object) ? null : baseType.Name;

        return new RecordTypeShape(
            type.Name,
            baseTypeName,
            RecordTypeNaming.IsRecordTypeName(type.Name),
            fields);
    }
}
