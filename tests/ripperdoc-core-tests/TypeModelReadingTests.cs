using System.Reflection;
using Ripperdoc.Core.Drift;
using WolvenKit.RED4.Types;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// What a reading's fingerprint counts as part of the reading.
/// </summary>
/// <remarks>
/// Built from plain records, so this runs where there is no game and no dump -
/// which is the point, since the fingerprint is what a check reproducing a
/// measured number holds its input against.
/// </remarks>
public class TypeModelReadingTests
{
    [Fact]
    public void TwoReadingsSayingTheSameThingFingerprintAlike()
    {
        Assert.Equal(Reading().Fingerprint(), Reading().Fingerprint());
    }

    [Fact]
    public void AReadingThatCouldNotReadSomethingIsNotTheSameAsOneThatReadItAll()
    {
        // A description that yielded the same types and a different set of
        // things the reader could not take in is not the same input. Blind to
        // that, this would call them one - and a check that reproduces a number
        // measured against the first would believe the number applies to the
        // second, over types nobody compared.
        var read = Reading();
        var partlyRead = Reading("gameThing.value: the reader could not take this in");

        Assert.NotEqual(read.Fingerprint(), partlyRead.Fingerprint());
    }

    [Fact]
    public void WhichThingsCouldNotBeReadChangesTheFingerprintToo()
    {
        Assert.NotEqual(
            Reading("gameThing.value: one reason").Fingerprint(),
            Reading("gameThing.value: another reason").Fingerprint());
    }

    [Fact]
    public void AMemberBeyondTheSignedRangeIsReadRatherThanRefused()
    {
        // A 64-bit unsigned enumeration is the only kind that can hold a value
        // no signed 64-bit number can, and converting one throws. Reading a
        // member is not a place this reader may fail: a member it could not
        // read is a member the audit cannot find drift in, and the whole point
        // of the audit is to find it.
        Assert.Equal(-1L, TypeModelReading.AsComparableValue(ulong.MaxValue));
        Assert.Equal(long.MinValue, TypeModelReading.AsComparableValue(0x8000_0000_0000_0000UL));
    }

    [Fact]
    public void MembersInsideTheSignedRangeAreReadAsThemselves()
    {
        // The other arm. Every underlying type but the 64-bit unsigned one fits
        // in a long with room over, and none of them may be reinterpreted into
        // something else on the way.
        Assert.Equal(255L, TypeModelReading.AsComparableValue((byte)255));
        Assert.Equal(-1L, TypeModelReading.AsComparableValue((sbyte)-1));
        Assert.Equal(65_535L, TypeModelReading.AsComparableValue((ushort)65_535));
        Assert.Equal(-1L, TypeModelReading.AsComparableValue(-1));
        Assert.Equal(4_294_967_295L, TypeModelReading.AsComparableValue(4_294_967_295U));
        Assert.Equal(long.MaxValue, TypeModelReading.AsComparableValue(long.MaxValue));
        Assert.Equal(1L, TypeModelReading.AsComparableValue(1UL));
    }

    [Fact]
    public void EveryUnderlyingTypeAnEnumerationCanHaveIsRead()
    {
        // Named rather than assumed: if a runtime ever allowed another, this is
        // where the reader's coverage of them stops being complete.
        foreach (var underlying in new object[]
                 {
                     (byte)1, (sbyte)1, (short)1, (ushort)1, 1, 1U, 1L, 1UL,
                 })
        {
            Assert.Equal(1L, TypeModelReading.AsComparableValue(underlying));
        }
    }

    [Fact]
    public void TwoPropertiesStoredUnderOneNameLeaveTheSecondStatedRatherThanSubstituted()
    {
        // The audit addresses a property by the name it is stored under, so two
        // properties sharing one cannot both be in the reading. Keeping
        // whichever the runtime handed over last would decide it by reflection
        // order, and the audit would then report a type difference on a
        // property the model carries correctly - or agreement about a name the
        // model is ambiguous on - with nothing anywhere saying a property had
        // been dropped.
        var alone = Read(typeof(ProbeStoredNameAlone)).Properties[SharedStoredName];
        var otherAlone = Read(typeof(ProbeStoredNameAloneOther)).Properties[SharedStoredName];
        Assert.NotEqual(alone, otherAlone);

        var failures = new List<string>();
        var read = Read(typeof(ProbeDuplicateStoredName), failures);

        var kept = Assert.Single(read.Properties);
        Assert.Equal(SharedStoredName, kept.Key);
        Assert.Equal(
            DeclaredFirst(typeof(ProbeDuplicateStoredName)) == nameof(ProbeDuplicateStoredName.Bar)
                ? alone
                : otherAlone,
            kept.Value);

        var stated = Assert.Single(failures);
        Assert.Contains(
            $"{nameof(ProbeDuplicateStoredName)}.{SharedStoredName}",
            stated,
            StringComparison.Ordinal);
        Assert.Contains(
            "more than one property in the model is stored under this name; the first was kept",
            stated,
            StringComparison.Ordinal);
    }

    /// <summary>The stored name the probe types deliberately share.</summary>
    private const string SharedStoredName = "bar";

    private static ModelClass Read(Type type, List<string>? failures = null)
    {
        var classes = new Dictionary<string, ModelClass>(StringComparer.Ordinal);
        TypeModelReading.ReadClass(type, classes, failures ?? []);

        return classes[type.Name];
    }

    /// <summary>
    /// Which annotated property the runtime hands over first, asked of the same
    /// call the reader asks. Nothing promises an order across runtimes, so the
    /// check works out which one "the first" is here rather than assuming the
    /// declaration order is it.
    /// </summary>
    private static string DeclaredFirst(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .First(property => property.GetCustomAttribute<REDAttribute>() is not null)
        .Name;

    private static TypeModelReading Reading(params string[] failures) =>
        new("a description constructed for this test",
            new Dictionary<string, ModelClass>(StringComparer.Ordinal)
            {
                ["gameThing"] = new(
                    "gameThing",
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "Float" }),
            },
            new Dictionary<string, ModelEnum>(StringComparer.Ordinal),
            failures);
}
