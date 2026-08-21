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

        var recordTypes = _types
            .Where(IsUsableRedClass)
            .Where(type => RecordTypeNaming.IsRecordTypeName(type.Name))
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        foreach (var recordType in recordTypes)
        {
            // Ancestors are followed rather than looked up, so a chain cannot
            // end early just because an ancestor was not in the type list. A
            // field an ancestor declares is a field the record carries.
            for (Type? type = recordType; type is not null && type != typeof(object); type = type.BaseType)
            {
                if (seen.TryGetValue(type.Name, out var alreadyRead))
                {
                    if (alreadyRead != type)
                    {
                        // The schema addresses types by name, so two types
                        // sharing one name cannot both be in it. Reported here
                        // rather than resolved by arrival order, which would
                        // silently give one type the other's fields.
                        failures.Add(new DerivationFailure(
                            type.Name,
                            null,
                            $"Two different types are both named '{type.Name}' ("
                            + $"'{alreadyRead.FullName}' and '{type.FullName}'); the first was kept and this "
                            + "chain was not followed past it."));
                    }

                    break;
                }

                seen[type.Name] = type;
                shapes[type.Name] = ShapeOf(type, failures);
            }
        }

        return new RecordTypeSourceReading(
            shapes.Values.OrderBy(shape => shape.TypeName, StringComparer.Ordinal).ToArray(),
            failures);
    }

    private static bool IsUsableRedClass(Type type) =>
        type.IsPublic
        && !type.IsGenericTypeDefinition
        && typeof(RedBaseClass).IsAssignableFrom(type);

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
                // The whole method rests on the type model resolving every
                // annotated property to a storage type. One that does not is
                // reported and left out, because a field carried with a guessed
                // storage type would validate against nothing and be believed
                // anyway.
                failures.Add(new DerivationFailure(
                    type.Name,
                    property.Name,
                    $"The type model does not resolve '{property.PropertyType.Name}' to a storage type: "
                    + exception.Message));
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
