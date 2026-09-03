using System.Reflection;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The one place a spelling is indexed to the thing that answers to it, and the
/// two readers held to going through it.
/// </summary>
public sealed class SpellingIndexTests
{
    /// <summary>
    /// The readers this holds: everything the manager-state namespace declares,
    /// read from the assembly rather than listed here.
    /// </summary>
    /// <remarks>
    /// A list kept by hand is the shape of the thing this primitive closes - a
    /// site nobody pointed at, passing every check while carrying the defect -
    /// so a reader added to that namespace is held without anyone remembering
    /// to add it. Two kinds are out. The primitive is out because being the one
    /// place an index is made is what the rest of this holds it to. Nested
    /// types are out because a nested type is part of its declaring type's
    /// implementation rather than a reader in its own right - which is also why
    /// a compiler-generated closure, a member's locals declared as fields, does
    /// not count as a declaration here.
    /// </remarks>
    private static readonly Type[] Readers =
        [.. typeof(ManagerStateReading).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ManagerStateReading).Namespace)
            .Where(type => !type.IsNested && type != typeof(SpellingIndex<>))
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// The one member the sweep names rather than flags, at the four sites that
    /// are it: the property, the field behind it, the constructor parameter
    /// that fills it, and the getter's return.
    /// </summary>
    /// <remarks>
    /// A database's key-to-value map is not an index from a spelling: a
    /// spelling is a string some document wrote about a file and can name two
    /// things at once, and a database key cannot. Named by its declaring type
    /// and member rather than by its shape or its value type, so a second
    /// string-keyed map declared on the same type is still flagged; the sweep
    /// asserts each of these sites still carries a map, so an exemption that
    /// outlived its member is a failure rather than a silence.
    /// </remarks>
    private static readonly string[] TheDatabasesOwnValues =
    [
        $"{nameof(StateDatabase)}..ctor(values)",
        $"{nameof(StateDatabase)}.{nameof(StateDatabase.Values)}",
        $"{nameof(StateDatabase)}.get_{nameof(StateDatabase.Values)} returns",
        $"{nameof(StateDatabase)}.values",
    ];

    /// <summary>
    /// A spelling two things answer to is in neither the index nor an answer,
    /// and is named under the field that spelled it.
    /// </summary>
    [Fact]
    public void ASpellingTwoThingsAnswerToIsOutOfTheIndexAndNamed()
    {
        var index = SpellingIndex<string>.Of(
            "fileMD5", [("shared", "mod-a"), ("shared", "mod-b")]);

        Assert.False(index.Names("shared", out _));
        Assert.Equal(["fileMD5 'shared'"], index.Contested);
    }

    /// <summary>
    /// The same spellings made distinct answer, so the check above turns on the
    /// collision rather than on the shape of the input.
    /// </summary>
    [Fact]
    public void ASpellingOneThingAnswersToIsTheAnswer()
    {
        var index = SpellingIndex<string>.Of(
            "fileMD5", [("shared", "mod-a"), ("its-own", "mod-b")]);

        Assert.True(index.Names("shared", out var named));
        Assert.Equal("mod-a", named);
        Assert.Empty(index.Contested);
    }

    /// <summary>
    /// One thing named twice under one spelling resolves to itself however many
    /// times it is written down, so it is not a contest.
    /// </summary>
    [Fact]
    public void OneThingAnsweringTwiceToItsOwnSpellingIsNotAContest()
    {
        var index = SpellingIndex<string>.Of(
            "archiveId", [("shared", "mod-a"), ("shared", "mod-a")]);

        Assert.True(index.Names("shared", out var named));
        Assert.Equal("mod-a", named);
        Assert.Empty(index.Contested);
    }

    /// <summary>
    /// Two spellings differing only in case are two spellings, so neither
    /// contests the other and neither answers for the other.
    /// </summary>
    [Fact]
    public void SpellingsDifferingOnlyInCaseAreTwoSpellings()
    {
        var index = SpellingIndex<string>.Of(
            "fileMD5", [("Shared", "mod-a"), ("shared", "mod-b")]);

        Assert.Empty(index.Contested);
        Assert.True(index.Names("Shared", out var written));
        Assert.Equal("mod-a", written);
        Assert.True(index.Names("shared", out var lowered));
        Assert.Equal("mod-b", lowered);
        Assert.False(index.Names("SHARED", out _));
    }

    /// <summary>
    /// A spelling no document wrote down is neither in the index nor a contest,
    /// and asking with one is answered rather than refused.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ASpellingNoDocumentWroteDownNamesNothing(string? unwritten)
    {
        var index = SpellingIndex<string>.Of(
            "logicalFilename", [(unwritten, "mod-a"), ("written-down", "mod-b")]);

        Assert.False(index.Names(unwritten, out _));
        Assert.Empty(index.Contested);
        Assert.True(index.Names("written-down", out var named));
        Assert.Equal("mod-b", named);
    }

    /// <summary>
    /// Two contested spellings are named in one order whatever order they were
    /// read in, so a caller comparing two readings compares the reports rather
    /// than an enumeration.
    /// </summary>
    [Fact]
    public void ContestedSpellingsAreNamedInOneOrder()
    {
        var index = SpellingIndex<int>.Of(
            "name", [("zed", 0), ("alpha", 1), ("zed", 2), ("alpha", 3)]);

        Assert.Equal(["name 'alpha'", "name 'zed'"], index.Contested);
    }

    /// <summary>
    /// An index is a thing only the primitive makes: every constructor is
    /// private, and the factory is the only member that hands one out.
    /// </summary>
    /// <remarks>
    /// This is the half that makes the sweep below mean something. A site could
    /// otherwise satisfy a typed signature with an index it built itself, and
    /// the rule would be back to being a habit.
    /// </remarks>
    [Fact]
    public void AnIndexIsMadeInOnePlace()
    {
        var index = typeof(SpellingIndex<string>);

        Assert.All(
            index.GetConstructors(Declared),
            made => Assert.True(
                made.IsPrivate,
                $"{index.Name} has a constructor a site outside it can call, so an index "
                + "need not have come from the primitive."));

        Assert.Equal(
            [nameof(SpellingIndex<string>.Of)],
            index.GetMethods(Declared)
                .Where(member => member.ReturnType == index)
                .Select(member => member.Name)
                .Concat(index.GetFields(Declared)
                    .Where(field => field.FieldType == index)
                    .Select(field => field.Name))
                .Concat(index.GetProperties(Declared)
                    .Where(property => property.PropertyType == index)
                    .Select(property => property.Name))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Neither reader declares an index from a spelling as a plain dictionary,
    /// so a site that built one by hand would have nothing in them to hand it
    /// to.
    /// </summary>
    /// <remarks>
    /// The sweep reads signatures - fields, properties, and the parameters and
    /// return types of constructors and methods - and flags a string-keyed map
    /// of any value shape, unwrapping arrays and generic arguments to find one.
    /// Three things stay outside it, and all are stated rather than left to be
    /// discovered: a map that never leaves the member that built it, a map a
    /// nested type declares, which is its declaring type's implementation
    /// rather than anything that type's own signatures hand on, and the one
    /// member named in <see cref="TheDatabasesOwnValues" />.
    /// <para>
    /// What this holds is the type an index has, never what a site feeds
    /// <see cref="SpellingIndex{T}.Of" />: a site that collapsed its own
    /// spellings first and handed over what survived would pass this sweep
    /// carrying the defect. The contested arm each reader ships through the
    /// member that answers with its index is the floor under that, and this
    /// sweep is not a substitute for one.
    /// </para>
    /// </remarks>
    [Fact]
    public void NeitherReaderDeclaresAnIndexBySpellingAsAPlainDictionary()
    {
        var found = Readers.SelectMany(Signatures)
            .Where(carrier => IsAPlainIndexBySpelling(carrier.Type))
            .Select(carrier => carrier.Where)
            .ToList();

        var carried = found
            .Where(where => !TheDatabasesOwnValues.Contains(where, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            carried.Count == 0,
            "an index from a spelling is the primitive's type and not a dictionary, so that a "
            + "site building its own cannot hand it on. These carry one: "
            + string.Join(", ", carried));

        Assert.Equal(
            TheDatabasesOwnValues.Order(StringComparer.Ordinal),
            found.Where(where => TheDatabasesOwnValues.Contains(where, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The sweep reads what the readers actually declare, so a set that
    /// resolved to nothing, or to types declaring nothing, would pass it having
    /// read nothing.
    /// </summary>
    /// <remarks>
    /// The two named here are the readers the primitive was extracted from. A
    /// derived set that stopped resolving them would be a sweep still reporting
    /// green over a namespace it had lost, so they are held by name as well as
    /// derived.
    /// </remarks>
    [Fact]
    public void TheSweepReadsSignaturesInEveryReaderTheNamespaceDeclares()
    {
        Type[] named = [typeof(CollectionManifest), typeof(ManagerStateReading)];

        Assert.All(named, reader => Assert.Contains(reader, Readers));

        Assert.All(
            Readers,
            reader => Assert.NotEmpty(Signatures(reader)));

        Assert.All(
            named,
            reader => Assert.Contains(
                Signatures(reader),
                carrier => carrier.Type.IsGenericType
                    && carrier.Type.GetGenericTypeDefinition() == typeof(SpellingIndex<>)));
    }

    /// <remarks>
    /// Any string-keyed map, whatever it names and however it is wrapped. A
    /// vocabulary of shapes kept by hand is a list the next shape is not on:
    /// the concrete class, the frozen one, an array of them and a list of them
    /// are all the same thing to a site that wants to hand one on, and so is a
    /// map to any type at all.
    /// </remarks>
    private static bool IsAPlainIndexBySpelling(Type type) =>
        Unwrapped(type).Any(KeyedBySpelling);

    private static IEnumerable<Type> Unwrapped(Type type)
    {
        yield return type;

        Type[] inside = type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments();

        foreach (var held in inside.SelectMany(Unwrapped))
        {
            yield return held;
        }
    }

    private static bool KeyedBySpelling(Type type) =>
        type.GetInterfaces().Append(type).Any(face => face.IsGenericType
            && (face.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                || face.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            && face.GetGenericArguments()[0] == typeof(string));

    private static IReadOnlyList<(string Where, Type Type)> Signatures(Type reader)
    {
        var carried = new List<(string, Type)>();

        foreach (var field in reader.GetFields(Declared))
        {
            carried.Add(($"{reader.Name}.{field.Name}", Held(field.FieldType)));
        }

        foreach (var property in reader.GetProperties(Declared))
        {
            carried.Add(($"{reader.Name}.{property.Name}", Held(property.PropertyType)));
        }

        foreach (var member in reader.GetConstructors(Declared)
            .Cast<MethodBase>()
            .Concat(reader.GetMethods(Declared)))
        {
            foreach (var parameter in member.GetParameters())
            {
                carried.Add((
                    $"{reader.Name}.{member.Name}({parameter.Name})",
                    Held(parameter.ParameterType)));
            }

            if (member is MethodInfo returning)
            {
                carried.Add((
                    $"{reader.Name}.{member.Name} returns", Held(returning.ReturnType)));
            }
        }

        return carried;
    }

    /// <remarks>
    /// An out or ref parameter carries its type by reference, and the reference
    /// is not the thing the signature is about.
    /// </remarks>
    private static Type Held(Type type) => type.IsByRef ? type.GetElementType()! : type;
}
