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
    /// one - the types it carries and the things it could not read alike, since
    /// both are part of what a reading says.
    /// </para>
    /// <para>
    /// Everything is put in a fixed order first, because neither description
    /// promises one. Ordered by arrival, the same description read on two
    /// machines could fingerprint differently, and a check built on it would
    /// report drift that is really just a difference in what order types were
    /// handed over.
    /// </para>
    /// <para>
    /// Ordering the input fixes what this can fix, and not what it cannot.
    /// A reading taken from generated type information is stable, because the
    /// files it comes from say the same thing every time they are read. A
    /// reading reflected out of the compiled model is not: for a small number
    /// of properties the model's answer depends on what else in it has been
    /// resolved first, and that is settled once per process - so two readings
    /// in one process agree, and two processes running the same build need not.
    /// </para>
    /// <para>
    /// So this value identifies a reading and not a build. What identifies a
    /// build is <see cref="PinnedAssemblyIdentity"/>, which is a property of the
    /// file on disk and cannot move while the file does not, and that is what a
    /// machine with nothing to compare against checks. This is written down too,
    /// and used only where the comparison happens: a run whose reading differs
    /// from the one an accepted result came out of announces the comparison as
    /// not run rather than reporting drift nobody caused.
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

        // What could not be read is part of what this reading says. Two
        // descriptions that yielded the same types and a different set of
        // things the reader could not take in are not the same input, and a
        // fingerprint blind to that would call them one - which is the reading
        // this file's sibling in the schema layer takes, for the same reason.
        // The failures are already in a fixed order.
        foreach (var failure in Failures)
        {
            builder.Append(failure).Append(EndOfType);
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
        var identity = PinnedAssemblyIdentity();

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
            $"{name.Name} {name.Version?.ToString(3) ?? "unknown version"}",
            identity);
    }

    /// <summary>
    /// What identifies the build of the pinned dependency this engine is
    /// running against.
    /// </summary>
    /// <returns>The module's identity, lower-case hexadecimal.</returns>
    /// <remarks>
    /// <para>
    /// The compiler writes this into the assembly when it builds it, so it
    /// names one build of the dependency and changes whenever that build does.
    /// It is read from the file rather than derived from anything the file
    /// says, which is what makes it the same answer on every run and on every
    /// machine holding the same file - and therefore the one thing about the
    /// compiled model that a committed record can honestly be keyed on.
    /// </para>
    /// <para>
    /// What it cannot see is the model resolving differently while the file
    /// stays put. That question is not one a machine without generated type
    /// information could answer anyway; the audit answers it where the
    /// generated information is, by comparing the two descriptions outright.
    /// </para>
    /// </remarks>
    public static string PinnedAssemblyIdentity() =>
        typeof(RedBaseClass).Assembly.ManifestModule.ModuleVersionId.ToString("n", CultureInfo.InvariantCulture);

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

        // A key the reader does not read is a statement the generated side
        // makes and this reading does not carry, so it belongs among the
        // failures rather than only on the model it was read into. The audit
        // compares what is here; anything the reader dropped is a place it
        // could not have found drift, and a receipt counting read failures
        // would otherwise report none while the reader had silently skipped
        // whatever those keys say.
        foreach (var key in model.UnrecognisedKeys)
        {
            failures.Add(
                $"{key}: the generated type information carries this key and this reader does not read it, "
                + "so whatever it says was not compared.");
        }

        // And what the model could not take in at all. A type missing from this
        // side is one the audit will report as absent from the compiled model's
        // opposite number or not report at all, and either way it is drift the
        // audit did not really look for.
        failures.AddRange(model.ReadFailures);

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

    /// <summary>
    /// An enumeration member's underlying value, as the comparison vocabulary
    /// carries it.
    /// </summary>
    /// <param name="underlyingValue">
    /// The member's value, already converted to its enumeration's underlying
    /// type.
    /// </param>
    /// <returns>The same bits, as a signed 64-bit number.</returns>
    /// <remarks>
    /// Of the types an enumeration can be built on, only a 64-bit unsigned one
    /// can hold a value the signed range cannot - every other fits in a long
    /// with room over. That case is reinterpreted rather than converted:
    /// converting it throws, and the two descriptions of the game are compared
    /// on the bits a value occupies rather than on the sign a language chose to
    /// print them with, so the bits are what has to survive to the comparison.
    /// Reading a member is not a place this reader is allowed to fail - a member
    /// it could not read is a member the audit cannot find drift in.
    /// </remarks>
    internal static long AsComparableValue(object underlyingValue) => underlyingValue switch
    {
        ulong beyondTheSignedRange => unchecked((long)beyondTheSignedRange),
        long alreadySigned => alreadySigned,
        _ => Convert.ToInt64(underlyingValue, System.Globalization.CultureInfo.InvariantCulture),
    };

    private static void ReadEnum(Type type, Dictionary<string, ModelEnum> into, List<string> failures)
    {
        var members = new List<ModelEnumMember>();

        foreach (var member in Enum.GetNames(type))
        {
            members.Add(new ModelEnumMember(
                member,
                AsComparableValue(Convert.ChangeType(
                    Enum.Parse(type, member),
                    Enum.GetUnderlyingType(type),
                    System.Globalization.CultureInfo.InvariantCulture))));
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
/// <param name="AssemblyIdentity">
/// What identifies the build of the dependency the reading came out of, as
/// <see cref="TypeModelReading.PinnedAssemblyIdentity"/> gives it.
/// </param>
public sealed record CompiledTypeModelReading(
    TypeModelReading Reading,
    string DependencyVersion,
    string AssemblyIdentity);

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
