using Ripperdoc.Core.Schema;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The transform, on readings constructed in memory.
/// </summary>
/// <remarks>
/// Every case here is a way a reading can be malformed, because the transform's
/// job is to resolve inheritance without losing anything quietly - and the only
/// way to know it does not lose things quietly is to hand it something broken
/// and check that it says so.
/// </remarks>
public class RecordSchemaDerivationTests
{
    private const string Source = "a reading constructed for this test";

    [Fact]
    public void ARecordTypeCarriesTheFieldsItsAncestorsDeclare()
    {
        var schema = Derive(
            Type("gamedataBase_Record", null, Field("shared", "Float")),
            Type("gamedataThing_Record", "gamedataBase_Record", Field("own", "Bool")));

        var thing = schema.Find("gamedataThing_Record");

        Assert.NotNull(thing);
        Assert.Equal(new[] { "own", "shared" }, thing!.Fields.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("gamedataBase_Record", thing.Fields["shared"].DeclaringTypeName);
        Assert.Equal("gamedataThing_Record", thing.Fields["own"].DeclaringTypeName);
        Assert.Empty(schema.Failures);
    }

    [Fact]
    public void TheNearestDeclarationOfARepeatedFieldWins()
    {
        var schema = Derive(
            Type("gamedataBase_Record", null, Field("value", "Float")),
            Type("gamedataThing_Record", "gamedataBase_Record", Field("value", "Int32")));

        var value = schema.Find("gamedataThing_Record")!.Fields["value"];

        Assert.Equal("Int32", value.StorageType);
        Assert.Equal("gamedataThing_Record", value.DeclaringTypeName);
        Assert.Empty(schema.Failures);
    }

    [Fact]
    public void AnAncestorThatIsNotItselfARecordTypeStillContributesItsFields()
    {
        var schema = Derive(
            NonRecordType("SomeBaseClass", null, Field("inherited", "CName")),
            Type("gamedataThing_Record", "SomeBaseClass", Field("own", "Bool")));

        Assert.Equal(new[] { "gamedataThing_Record" }, schema.RecordTypeNames);
        Assert.Contains("inherited", schema.Find("gamedataThing_Record")!.Fields.Keys);
        Assert.Null(schema.Find("SomeBaseClass"));
        Assert.Empty(schema.Failures);
    }

    [Fact]
    public void AChainThatLeavesTheReadingIsReportedRatherThanTruncatedQuietly()
    {
        var schema = Derive(
            Type("gamedataThing_Record", "AbsentBase", Field("own", "Bool")));

        var failure = Assert.Single(schema.Failures);
        Assert.Equal("gamedataThing_Record", failure.TypeName);
        Assert.Contains("AbsentBase", failure.Reason, StringComparison.Ordinal);

        // What could be resolved is still resolved; the reading is not thrown
        // away because part of it was unusable.
        Assert.Contains("own", schema.Find("gamedataThing_Record")!.Fields.Keys);
    }

    [Fact]
    public void AChainThatDoesNotTerminateIsReportedRatherThanFollowedForever()
    {
        var schema = Derive(
            Type("gamedataFirst_Record", "gamedataSecond_Record", Field("first", "Bool")),
            Type("gamedataSecond_Record", "gamedataFirst_Record", Field("second", "Bool")));

        Assert.Equal(2, schema.Failures.Count);
        Assert.All(schema.Failures, failure => Assert.Contains("does not terminate", failure.Reason, StringComparison.Ordinal));

        // Both fields are still reachable: the walk stops when it returns to a
        // type it has already taken the fields from, not before.
        Assert.Equal(2, schema.Find("gamedataFirst_Record")!.Fields.Count);
    }

    [Fact]
    public void AFieldWithNoNameIsReportedRatherThanCarried()
    {
        var schema = Derive(
            Type("gamedataThing_Record", null, Field("", "Float"), Field("named", "Bool")));

        var failure = Assert.Single(schema.Failures);
        Assert.Contains("has no name", failure.Reason, StringComparison.Ordinal);
        Assert.Equal(new[] { "named" }, schema.Find("gamedataThing_Record")!.Fields.Keys);
    }

    [Fact]
    public void AFieldDeclaredTwiceOnOneTypeIsReported()
    {
        var schema = Derive(
            Type("gamedataThing_Record", null, Field("value", "Float"), Field("value", "Bool")));

        var failure = Assert.Single(schema.Failures);
        Assert.Equal("value", failure.MemberName);
        Assert.Equal("Float", schema.Find("gamedataThing_Record")!.Fields["value"].StorageType);
    }

    [Fact]
    public void ATypeDeclaredTwiceIsReported()
    {
        var schema = Derive(
            Type("gamedataThing_Record", null, Field("first", "Float")),
            Type("gamedataThing_Record", null, Field("second", "Bool")));

        var failure = Assert.Single(schema.Failures);
        Assert.Contains("more than once", failure.Reason, StringComparison.Ordinal);
        Assert.Single(schema.RecordTypeNames);
    }

    [Fact]
    public void FailuresTheSourceReportedSurviveIntoTheSchema()
    {
        var reading = new RecordTypeSourceReading(
            new[] { Type("gamedataThing_Record", null, Field("own", "Bool")) },
            new[] { new DerivationFailure("gamedataThing_Record", "something", "the source could not read it") });

        var schema = RecordSchemaDerivation.Derive(reading, Source);

        Assert.Equal("the source could not read it", Assert.Single(schema.Failures).Reason);
    }

    [Fact]
    public void TheResultDoesNotDependOnTheOrderTypesArriveIn()
    {
        var types = new[]
        {
            Type("gamedataBase_Record", null, Field("shared", "Float")),
            Type("gamedataThing_Record", "gamedataBase_Record", Field("own", "Bool")),
            Type("gamedataOther_Record", "gamedataBase_Record", Field("other", "CName")),
        };

        var forwards = Derive(types);
        var backwards = Derive(types.Reverse().ToArray());

        Assert.Equal(forwards.RecordTypeNames, backwards.RecordTypeNames);
        Assert.Equal(forwards.ResolvedFieldSlotCount, backwards.ResolvedFieldSlotCount);
        Assert.Equal(
            forwards.Find("gamedataThing_Record")!.Fields.Keys.OrderBy(name => name, StringComparer.Ordinal),
            backwards.Find("gamedataThing_Record")!.Fields.Keys.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void DeclaredAndResolvedFieldsAreCountedSeparately()
    {
        var schema = Derive(
            Type("gamedataBase_Record", null, Field("shared", "Float")),
            Type("gamedataThing_Record", "gamedataBase_Record", Field("own", "Bool")),
            Type("gamedataOther_Record", "gamedataBase_Record"));

        // Two declarations across three record types; four slots once the
        // shared one is counted on each type that carries it.
        Assert.Equal(2, schema.DeclaredFieldCount);
        Assert.Equal(4, schema.ResolvedFieldSlotCount);
    }

    [Fact]
    public void AnUnknownTypeIsAbsentRatherThanEmpty()
    {
        var schema = Derive(Type("gamedataThing_Record", null, Field("own", "Bool")));

        Assert.Null(schema.Find("gamedataNeverHeardOf_Record"));
    }

    [Fact]
    public void TheSourceIsReadThroughItsOwnInterface()
    {
        var schema = RecordSchemaDerivation.Derive(
            new ReadingSource(
                new RecordTypeSourceReading(
                    new[] { Type("gamedataThing_Record", null, Field("own", "Bool")) },
                    Array.Empty<DerivationFailure>()),
                "a source built for this test"));

        Assert.Equal("a source built for this test", schema.SourceDescription);
        Assert.Single(schema.RecordTypeNames);
    }

    private static RecordSchema Derive(params RecordTypeShape[] types) =>
        RecordSchemaDerivation.Derive(
            new RecordTypeSourceReading(types, Array.Empty<DerivationFailure>()),
            Source);

    private static RecordTypeShape Type(string name, string? baseName, params RecordFieldShape[] fields) =>
        new(name, baseName, true, fields);

    private static RecordTypeShape NonRecordType(string name, string? baseName, params RecordFieldShape[] fields) =>
        new(name, baseName, false, fields);

    private static RecordFieldShape Field(string name, string storageType) => new(name, storageType);

    private sealed class ReadingSource(RecordTypeSourceReading reading, string description) : IRecordTypeSource
    {
        public string Description { get; } = description;

        public RecordTypeSourceReading Read() => reading;
    }
}
