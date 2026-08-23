using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripperdoc.Core.Dump;

/// <summary>
/// The game's own runtime type information, as a dump of it writes it out, read
/// once into memory.
/// </summary>
/// <remarks>
/// <para>
/// A dump is tens of thousands of small files and every consumer of it wants a
/// different cut - the record types, the whole class graph, the enums. Reading
/// it per query would re-walk the directory for each of them, so it is read
/// once here and every consumer is served from the result. That is the same
/// discipline the lookup-cost measurement bought elsewhere in this engine: the
/// convenient per-item call is the expensive one, and nothing about its surface
/// says so.
/// </para>
/// <para>
/// The reader states what it did not understand. A dump written by a newer
/// version of the tool that produced this one may carry keys this reader has
/// never seen, and a deserialiser's default answer to an unknown key is to drop
/// it - which would turn a format change into a quietly smaller model. Every
/// such key is collected and reported instead.
/// </para>
/// </remarks>
public sealed class DumpTypeModel
{
    private DumpTypeModel(
        IReadOnlyDictionary<string, DumpClass> classes,
        IReadOnlyDictionary<string, DumpEnum> enums,
        string description,
        IReadOnlyList<string> unrecognised)
    {
        Classes = classes;
        Enums = enums;
        Description = description;
        UnrecognisedKeys = unrecognised;
    }

    /// <summary>Every class and struct the dump describes, keyed by name.</summary>
    public IReadOnlyDictionary<string, DumpClass> Classes { get; }

    /// <summary>
    /// Every enumeration the dump describes, keyed by name.
    /// </summary>
    /// <remarks>
    /// Bitfields are in here too, marked by <see cref="DumpEnum.IsBitfield"/>.
    /// They are a separate directory in the dump and a separate concept in the
    /// game, but both are a name with named values, and the audit that compares
    /// them against a compiled type model asks the same question of both.
    /// </remarks>
    public IReadOnlyDictionary<string, DumpEnum> Enums { get; }

    /// <summary>
    /// What this model was read from, in words fit for a provenance block.
    /// Names the origin, never a machine path.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Keys the dump carried that this reader does not understand, in a stable
    /// order, each as the shape it appeared on and the key itself.
    /// </summary>
    /// <remarks>
    /// Expected to be empty. A name here does not mean the model is wrong; it
    /// means the dump says something this reader is not reading, which is worth
    /// knowing before a conclusion is drawn from what it did read.
    /// </remarks>
    public IReadOnlyList<string> UnrecognisedKeys { get; }

    /// <summary>
    /// Read a dump's JSON output.
    /// </summary>
    /// <param name="jsonDirectory">
    /// The dump's <c>json</c> directory - the one holding <c>classes</c>,
    /// <c>enums</c> and <c>bitfields</c>.
    /// </param>
    /// <param name="description">
    /// What this dump is, for a provenance block. Never a machine path.
    /// </param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory, or one of the three it must contain, is not there.
    /// </exception>
    public static DumpTypeModel Load(string jsonDirectory, string description)
    {
        ArgumentNullException.ThrowIfNull(jsonDirectory);
        ArgumentNullException.ThrowIfNull(description);

        var classesDirectory = Path.Combine(jsonDirectory, ClassesDirectoryName);
        var enumsDirectory = Path.Combine(jsonDirectory, EnumsDirectoryName);
        var bitfieldsDirectory = Path.Combine(jsonDirectory, BitfieldsDirectoryName);

        // Each of the three is required and each is named separately when it is
        // missing. A dump with no enums directory is a different problem from a
        // path that is not a dump at all, and one message covering both would
        // send a reader looking in the wrong place.
        RequireDirectory(jsonDirectory, "the dump's json output");
        RequireDirectory(classesDirectory, "the dump's class descriptions");
        RequireDirectory(enumsDirectory, "the dump's enumeration descriptions");
        RequireDirectory(bitfieldsDirectory, "the dump's bitfield descriptions");

        var unrecognised = new SortedSet<string>(StringComparer.Ordinal);

        var classes = new Dictionary<string, DumpClass>(StringComparer.Ordinal);
        foreach (var file in Files(classesDirectory))
        {
            var read = Read<ClassDocument>(file);
            CollectUnrecognised(read, unrecognised);
            classes[read.Name] = read.ToClass();
        }

        var enums = new Dictionary<string, DumpEnum>(StringComparer.Ordinal);
        foreach (var file in Files(enumsDirectory))
        {
            var read = Read<EnumDocument>(file);
            CollectUnrecognised(read, unrecognised);
            enums[read.Name] = read.ToEnum(isBitfield: false);
        }

        foreach (var file in Files(bitfieldsDirectory))
        {
            var read = Read<EnumDocument>(file);
            CollectUnrecognised(read, unrecognised);
            enums[read.Name] = read.ToEnum(isBitfield: true);
        }

        return new DumpTypeModel(classes, enums, description, unrecognised.ToArray());
    }

    private const string ClassesDirectoryName = "classes";
    private const string EnumsDirectoryName = "enums";
    private const string BitfieldsDirectoryName = "bitfields";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static IEnumerable<string> Files(string directory) =>
        Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal);

    private static void RequireDirectory(string path, string what)
    {
        if (!Directory.Exists(path))
        {
            // The path is in the exception because whoever sees this one is
            // holding the machine it names. Nothing that carries it travels
            // into an artifact - Description is what an artifact gets.
            throw new DirectoryNotFoundException(
                $"There is no directory at '{path}', so {what} cannot be read.");
        }
    }

    private static T Read<T>(string file)
    {
        using var stream = File.OpenRead(file);
        return JsonSerializer.Deserialize<T>(stream, ReadOptions)
            ?? throw new JsonException($"'{Path.GetFileName(file)}' holds no object.");
    }

    private static void CollectUnrecognised(IUnrecognisedKeys document, SortedSet<string> into)
    {
        foreach (var key in document.Unrecognised())
        {
            into.Add(key);
        }
    }

    private interface IUnrecognisedKeys
    {
        IEnumerable<string> Unrecognised();
    }

    private sealed class ClassDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("parent")]
        public string? Parent { get; init; }

        [JsonPropertyName("flags")]
        public long Flags { get; init; }

        [JsonPropertyName("props")]
        public List<PropertyDocument>? Props { get; init; }

        [JsonPropertyName("funcs")]
        public List<FunctionDocument>? Funcs { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        public IEnumerable<string> Unrecognised() =>
            Keys("class", Extra)
                .Concat((Props ?? []).SelectMany(property => property.Unrecognised()))
                .Concat((Funcs ?? []).SelectMany(function => function.Unrecognised()));

        public DumpClass ToClass() => new(
            Name,
            Parent,
            (Props ?? []).Select(property => new DumpProperty(property.Name, property.Type)).ToArray(),
            (Funcs ?? []).Select(function => function.ToFunction()).ToArray());
    }

    private sealed class PropertyDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("group")]
        public string? Group { get; init; }

        [JsonPropertyName("flags")]
        public long Flags { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        public IEnumerable<string> Unrecognised() => Keys("property", Extra);
    }

    private sealed class FunctionDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("fullName")]
        public string FullName { get; init; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; init; } = string.Empty;

        [JsonPropertyName("return")]
        public TypeReferenceDocument? Return { get; init; }

        [JsonPropertyName("params")]
        public List<ParameterDocument>? Params { get; init; }

        [JsonPropertyName("index")]
        public int? Index { get; init; }

        [JsonPropertyName("flags")]
        public long Flags { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        public IEnumerable<string> Unrecognised() =>
            Keys("function", Extra)
                .Concat(Return?.Unrecognised() ?? [])
                .Concat((Params ?? []).SelectMany(parameter => parameter.Unrecognised()));

        public DumpFunction ToFunction() => new(
            FullName,
            Return?.Type,
            (Params ?? []).Select(parameter => new DumpParameter(
                parameter.Name,
                parameter.Type,
                parameter.IsOutput)).ToArray());
    }

    private sealed class TypeReferenceDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("flags")]
        public long Flags { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        public IEnumerable<string> Unrecognised() => Keys("return", Extra);
    }

    private sealed class ParameterDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("flags")]
        public long Flags { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        /// <summary>
        /// Whether the dump marks this parameter as one the function writes its
        /// answer into rather than reads.
        /// </summary>
        /// <remarks>
        /// The flag is the dump's own statement about the parameter. Deciding it
        /// from the shape instead - a lone parameter on a function that returns
        /// nothing - would agree with the flag on the data seen so far and would
        /// be an inference either way, which is a worse thing to build a field
        /// set on than a marker the dump actually carries.
        /// </remarks>
        public bool IsOutput => (Flags & OutputParameterFlag) != 0;

        public IEnumerable<string> Unrecognised() => Keys("parameter", Extra);
    }

    private sealed class EnumDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("members")]
        public List<EnumMemberDocument>? Members { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        public IEnumerable<string> Unrecognised() =>
            Keys("enum", Extra).Concat((Members ?? []).SelectMany(member => member.Unrecognised()));

        public DumpEnum ToEnum(bool isBitfield) => new(
            Name,
            (Members ?? []).Select(member => new DumpEnumMember(member.Name, member.Value(isBitfield))).ToArray(),
            isBitfield);
    }

    private sealed class EnumMemberDocument : IUnrecognisedKeys
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public long? MemberValue { get; init; }

        [JsonPropertyName("bit")]
        public int? Bit { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }

        public IEnumerable<string> Unrecognised() => Keys("enum member", Extra);

        // An enumeration member states a value and a bitfield member states a
        // bit position. Both are turned into the number a stored value would
        // actually hold, so that a comparison against a compiled type model is
        // comparing the same kind of thing on both sides.
        //
        // A member carrying neither is refused rather than defaulted. Nothing
        // in this dump is in that state, and a default would put a member at
        // zero - a number an audit would then compare, disagree about, and
        // report as the game having changed.
        public long Value(bool isBitfield)
        {
            if (isBitfield)
            {
                return Bit is null
                    ? throw new JsonException(
                        $"Bitfield member '{Name}' states no bit, so the value it sets is not in this dump.")
                    : 1L << Bit.Value;
            }

            return MemberValue
                ?? throw new JsonException(
                    $"Enumeration member '{Name}' states no value, so what it stands for is not in this dump.");
        }
    }

    private const long OutputParameterFlag = 512;

    private static IEnumerable<string> Keys(string shape, Dictionary<string, JsonElement>? extra) =>
        extra is null ? [] : extra.Keys.Select(key => $"{shape}.{key}");
}

/// <summary>One class or struct, as the dump describes it.</summary>
/// <param name="Name">The type's name.</param>
/// <param name="ParentName">The type it derives from, or null at the root.</param>
/// <param name="Properties">
/// The fields the runtime registers on it. Record types have none of these -
/// their field surface is in <paramref name="Functions"/>.
/// </param>
/// <param name="Functions">The methods the runtime registers on it.</param>
public sealed record DumpClass(
    string Name,
    string? ParentName,
    IReadOnlyList<DumpProperty> Properties,
    IReadOnlyList<DumpFunction> Functions);

/// <summary>One registered property.</summary>
/// <param name="Name">The property's name.</param>
/// <param name="TypeName">The name of its type, in the game's own type language.</param>
public sealed record DumpProperty(string Name, string TypeName);

/// <summary>One registered method.</summary>
/// <param name="Name">The method's name.</param>
/// <param name="ReturnTypeName">
/// The name of the type it returns, or null where it returns nothing.
/// </param>
/// <param name="Parameters">Its parameters, in order.</param>
public sealed record DumpFunction(
    string Name,
    string? ReturnTypeName,
    IReadOnlyList<DumpParameter> Parameters);

/// <summary>One method parameter.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="TypeName">The name of its type.</param>
/// <param name="IsOutput">
/// Whether the method writes its answer into this parameter rather than reading
/// it.
/// </param>
public sealed record DumpParameter(string Name, string TypeName, bool IsOutput);

/// <summary>One enumeration or bitfield, as the dump describes it.</summary>
/// <param name="Name">The enumeration's name.</param>
/// <param name="Members">Its members, in the order the dump lists them.</param>
/// <param name="IsBitfield">
/// Whether the dump described this as a bitfield rather than an enumeration.
/// </param>
public sealed record DumpEnum(string Name, IReadOnlyList<DumpEnumMember> Members, bool IsBitfield);

/// <summary>One member of an enumeration or bitfield.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="Value">
/// Its value. A bitfield member's bit position is carried here as the value
/// that bit sets, so that both kinds of member compare the same way.
/// </param>
public sealed record DumpEnumMember(string Name, long Value);
