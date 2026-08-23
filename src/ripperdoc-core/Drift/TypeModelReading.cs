using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ripperdoc.Core.Dump;
using Ripperdoc.Core.Schema;
using WolvenKit.RED4.Types;

namespace Ripperdoc.Core.Drift;

/// <summary>
/// A description of the game's types, in the one vocabulary both descriptions
/// of them are expressed in.
/// </summary>
/// <remarks>
/// <para>
/// The engine has two descriptions of the same game: one compiled into the
/// dependency it is pinned to, and one generated from an install. Auditing the
/// first against the second only means anything if both are read into the same
/// shape first - otherwise a difference in how each is written reads as a
/// difference in what each says.
/// </para>
/// <para>
/// What is not here is as deliberate as what is. Accessors are absent because
/// the compiled model has none to compare against, and a reading that carried
/// them on one side only would invite a comparison that always fails.
/// </para>
/// </remarks>
/// <param name="Description">
/// What this was read from, in words fit for a provenance block.
/// </param>
/// <param name="Classes">Every class, keyed by name.</param>
/// <param name="Enums">Every enumeration and bitfield, keyed by name.</param>
/// <param name="Failures">
/// What could not be read into this vocabulary, in a stable order. Expected to
/// be empty; carried rather than dropped, because a type the audit never saw is
/// one it cannot have found drift in, and an audit reporting no drift over
/// types it never read would be the quiet wrong answer this whole check exists
/// to prevent.
/// </param>
public sealed record TypeModelReading(
    string Description,
    IReadOnlyDictionary<string, ModelClass> Classes,
    IReadOnlyDictionary<string, ModelEnum> Enums,
    IReadOnlyList<string> Failures)
{
    /// <summary>
    /// A fingerprint of everything this reading says.
    /// </summary>
    /// <returns>The fingerprint, lower-case hexadecimal.</returns>
    /// <remarks>
    /// <para>
    /// Two readings of the same description of the game give the same
    /// fingerprint, and any difference in what either says gives a different
    /// one. That is what lets a machine holding only the compiled model decide
    /// whether an audit recorded elsewhere was taken against the model it has.
    /// </para>
    /// <para>
    /// Everything is put in a fixed order first, because reflection does not
    /// promise one. Ordered by arrival, the same model on two machines could
    /// fingerprint differently, and the check built on it would report drift
    /// that is really just a difference in what order types were handed over.
    /// </para>
    /// </remarks>
    public string Fingerprint()
    {
        var builder = new StringBuilder();

        foreach (var name in Classes.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var type = Classes[name];
            builder.Append(type.Name).Append(Separator)
                .Append(type.ParentName ?? string.Empty).Append(Separator);

            foreach (var property in type.Properties.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                builder.Append(property.Key).Append(Separator)
                    .Append(property.Value).Append(Separator);
            }

            builder.Append(EndOfType);
        }

        foreach (var name in Enums.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var declared = Enums[name];
            builder.Append(declared.Name).Append(Separator)
                .Append(declared.ValueWidthInBits.ToString(CultureInfo.InvariantCulture)).Append(Separator);

            // Members are sorted for the fingerprint and kept in declaration
            // order everywhere else. Declaration order is not something either
            // description promises to preserve, and a fingerprint that moved
            // when it changed would report drift nobody made.
            foreach (var member in declared.Members
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ThenBy(member => member.Value))
            {
                builder.Append(member.Name).Append(Separator)
                    .Append(member.Value.ToString(CultureInfo.InvariantCulture)).Append(Separator);
            }

            builder.Append(EndOfType);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLower(CultureInfo.InvariantCulture);
    }

    // Parts of one entry, and the end of one type's contribution. Characters no
    // name or type can contain, so two different readings cannot be run
    // together into the same text and fingerprint alike.
    private const char Separator = '\u001f';
    private const char EndOfType = '\u001e';

    /// <summary>
    /// Read the type model compiled into the pinned dependency.
    /// </summary>
    /// <returns>The reading, and anything that could not be read.</returns>
    /// <remarks>
    /// Needs no game and nothing generated: the dependency is on disk wherever
    /// this engine builds, which is what lets the half of the audit that
    /// describes this side run anywhere.
    /// </remarks>
    public static CompiledTypeModelReading FromPinnedTypeModel()
    {
        var assembly = typeof(RedBaseClass).Assembly;
        var name = assembly.GetName();
        var failures = new List<string>();

        var classes = new Dictionary<string, ModelClass>(StringComparer.Ordinal);
        var enums = new Dictionary<string, ModelEnum>(StringComparer.Ordinal);

        foreach (var type in assembly.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsEnum)
            {
                ReadEnum(type, enums, failures);
                continue;
            }

            if (typeof(RedBaseClass).IsAssignableFrom(type) && !type.IsGenericTypeDefinition)
            {
                ReadClass(type, classes, failures);
            }
        }

        return new CompiledTypeModelReading(
            new TypeModelReading(
                $"{name.Name} {name.Version?.ToString(3) ?? "unknown version"} type model, reflected",
                classes,
                enums,
                failures.OrderBy(failure => failure, StringComparer.Ordinal).ToArray()),
            $"{name.Name} {name.Version?.ToString(3) ?? "unknown version"}");
    }

    /// <summary>
    /// Express generated type information in the comparison vocabulary.
    /// </summary>
    /// <param name="model">The generated type information.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static TypeModelReading From(DumpTypeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var failures = new List<string>();
        var classes = new Dictionary<string, ModelClass>(StringComparer.Ordinal);

        foreach (var type in model.Classes.Values)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in type.Properties)
            {
                if (!properties.TryAdd(property.Name, property.TypeName))
                {
                    failures.Add(
                        $"{type.Name}.{property.Name}: declared more than once on this class; the first was "
                        + "kept, so the audit compares one of them and not the other.");
                }
            }

            classes[type.Name] = new ModelClass(type.Name, type.ParentName, properties);
        }

        // Members stay a list. The game registers the same member name twice on
        // at least one enumeration, with a different value each time, so a name
        // does not identify a member on this side and keying by one would drop
        // whichever arrived second - which is exactly the kind of member this
        // audit exists to notice.
        var enums = model.Enums.Values.ToDictionary(
            declared => declared.Name,
            declared => new ModelEnum(
                declared.Name,
                declared.Members.Select(member => new ModelEnumMember(member.Name, member.Value)).ToArray(),
                ValueWidthNotStated),
            StringComparer.Ordinal);

        return new TypeModelReading(
            model.Description,
            classes,
            enums,
            failures.OrderBy(failure => failure, StringComparer.Ordinal).ToArray());
    }

    private static void ReadClass(Type type, Dictionary<string, ModelClass> into, List<string> failures)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var annotation = property.GetCustomAttribute<REDAttribute>();
            if (annotation is null)
            {
                continue;
            }

            var flags = annotation.Flags is null ? Flags.Empty : new Flags(annotation.Flags);

            string storedType;
            try
            {
                storedType = RedReflection.GetRedTypeFromCSType(property.PropertyType, flags);
            }
            catch (Exception exception)
            {
                failures.Add($"{type.Name}.{property.Name}: {exception.Message}");
                continue;
            }

            if (!StorageTypeName.IsUsable(storedType))
            {
                failures.Add(
                    $"{type.Name}.{property.Name}: the type model resolves '{property.PropertyType.Name}' to "
                    + $"'{storedType}', which names no type.");
                continue;
            }

            properties[string.IsNullOrEmpty(annotation.Name) ? property.Name : annotation.Name] = storedType;
        }

        var baseType = type.BaseType;
        var parent = baseType is null || baseType == typeof(object) ? null : baseType.Name;

        // A name already read is kept and the second is stated. The audit
        // addresses types by name, so two types sharing one cannot both be in
        // the reading, and picking silently by arrival order would compare the
        // game's type against whichever happened to load first.
        if (!into.TryAdd(type.Name, new ModelClass(type.Name, parent, properties)))
        {
            failures.Add($"{type.Name}: more than one type in the model has this name; the first was kept.");
        }
    }

    /// <summary>
    /// The width the generated side does not state, and the compiled side does.
    /// </summary>
    internal const int ValueWidthNotStated = 0;

    private static void ReadEnum(Type type, Dictionary<string, ModelEnum> into, List<string> failures)
    {
        var members = new List<ModelEnumMember>();

        foreach (var member in Enum.GetNames(type))
        {
            // Read through the underlying type rather than by casting, because
            // an enumeration whose values do not fit a signed conversion would
            // otherwise throw here rather than be compared.
            var value = Convert.ToInt64(
                Convert.ChangeType(
                    Enum.Parse(type, member),
                    Enum.GetUnderlyingType(type),
                    System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.InvariantCulture);

            members.Add(new ModelEnumMember(member, value));
        }

        // How many bits a value of this enumeration occupies. The two sides
        // render the same bits differently once the top one is set - one writes
        // the signed number and the other the unsigned - so a comparison needs
        // to know how wide the thing being compared is. Only this side knows;
        // the generated side states a number and no width.
        var width = Type.GetTypeCode(Enum.GetUnderlyingType(type)) switch
        {
            TypeCode.SByte or TypeCode.Byte => 8,
            TypeCode.Int16 or TypeCode.UInt16 => 16,
            TypeCode.Int32 or TypeCode.UInt32 => 32,
            _ => 64,
        };

        if (!into.TryAdd(type.Name, new ModelEnum(type.Name, members, width)))
        {
            failures.Add($"{type.Name}: more than one enumeration in the model has this name; the first was kept.");
        }
    }
}

/// <summary>
/// The compiled model's reading, with what identifies the build it came from.
/// </summary>
/// <param name="Reading">The types, carrying whatever could not be read.</param>
/// <param name="DependencyVersion">
/// The pinned dependency's name and version, as the receipt records it.
/// </param>
public sealed record CompiledTypeModelReading(
    TypeModelReading Reading,
    string DependencyVersion);

/// <summary>One class, as either description of the game states it.</summary>
/// <param name="Name">The class's name.</param>
/// <param name="ParentName">The class it derives from, or null at the root.</param>
/// <param name="Properties">Its own declared properties, name to stored type.</param>
public sealed record ModelClass(
    string Name,
    string? ParentName,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>One enumeration or bitfield.</summary>
/// <param name="Name">The enumeration's name.</param>
/// <param name="Members">
/// Its members, in the order they were declared. A list rather than a map from
/// name to value, because the game registers one name twice with two values and
/// a map would silently keep one of them.
/// </param>
/// <param name="ValueWidthInBits">
/// How many bits a value of this enumeration occupies, or
/// <see cref="TypeModelReading.ValueWidthNotStated"/> where the description it
/// came from does not say.
/// </param>
public sealed record ModelEnum(
    string Name,
    IReadOnlyList<ModelEnumMember> Members,
    int ValueWidthInBits);

/// <summary>One member of an enumeration or bitfield.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="Value">What it stands for.</param>
public sealed record ModelEnumMember(string Name, long Value);
