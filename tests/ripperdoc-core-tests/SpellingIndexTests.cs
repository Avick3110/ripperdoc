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
    /// The readers this holds. The rule is theirs; a site in either of them
    /// that built its own index would be the fourth instance of the class this
    /// primitive closes.
    /// </summary>
    private static readonly Type[] Readers =
        [typeof(ManagerStateReading), typeof(CollectionManifest)];

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

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
    /// A spelling no document wrote down is neither in the index nor a contest,
    /// and asking with one is answered rather than refused.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ASpellingNoDocumentWroteDownNamesNothing(string? unwritten)
    {
        var index = SpellingIndex<string>.Of(
            "logicalFilename", [(unwritten, "mod-a"), (unwritten, "mod-b")]);

        Assert.False(index.Names(unwritten, out _));
        Assert.Empty(index.Contested);
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
    /// return types of constructors and methods. A dictionary that never leaves
    /// the member that built it is outside it, and so is a value shape other
    /// than the two an index by spelling has taken here.
    /// </remarks>
    [Fact]
    public void NeitherReaderDeclaresAnIndexBySpellingAsAPlainDictionary()
    {
        var carried = Readers.SelectMany(Signatures)
            .Where(carrier => IsAPlainIndexBySpelling(carrier.Type))
            .Select(carrier => carrier.Where)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            carried.Count == 0,
            "an index from a spelling is the primitive's type and not a dictionary, so that a "
            + "site building its own cannot hand it on. These carry one: "
            + string.Join(", ", carried));
    }

    /// <summary>
    /// The sweep reads what the two readers actually declare, so a reader that
    /// declared nothing would pass it having read nothing.
    /// </summary>
    [Fact]
    public void TheSweepReadsSignaturesInBothReaders()
    {
        Assert.Equal(
            [nameof(CollectionManifest), nameof(ManagerStateReading)],
            Readers.Select(reader => reader.Name).Order(StringComparer.Ordinal));

        Assert.All(
            Readers,
            reader => Assert.NotEmpty(Signatures(reader)));

        Assert.All(
            Readers,
            reader => Assert.Contains(
                Signatures(reader),
                carrier => carrier.Type.IsGenericType
                    && carrier.Type.GetGenericTypeDefinition() == typeof(SpellingIndex<>)));
    }

    private static bool IsAPlainIndexBySpelling(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var shape = type.GetGenericTypeDefinition();

        if (shape != typeof(Dictionary<,>)
            && shape != typeof(SortedDictionary<,>)
            && shape != typeof(IDictionary<,>)
            && shape != typeof(IReadOnlyDictionary<,>))
        {
            return false;
        }

        var arguments = type.GetGenericArguments();

        return arguments[0] == typeof(string)
            && (arguments[1] == typeof(string) || arguments[1] == typeof(int));
    }

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
