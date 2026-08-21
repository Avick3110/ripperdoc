using Ripperdoc.Core.Schema;
using WolvenKit.RED4.Types;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The no-setup source, on the pinned type model and on types written here.
/// </summary>
/// <remarks>
/// The counts against the pinned type model are the schema half of this wave's
/// exit criterion, and they run on a bare runner because reflecting over a
/// compiled type model needs no game and nothing generated from one.
/// </remarks>
public class ReflectedRecordTypeSourceTests
{
    [Fact]
    public void ThePinnedTypeModelYieldsTheRecordSurfaceItIsKnownToCarry()
    {
        var schema = RecordSchemaDerivation.Derive(ReflectedRecordTypeSource.FromPinnedTypeModel());

        Assert.Equal(965, schema.RecordTypeNames.Count);
        Assert.Equal(4687, schema.DeclaredFieldCount);
        Assert.Equal(7255, schema.ResolvedFieldSlotCount);
    }

    [Fact]
    public void TheUntypedReferenceEdgesInThePinnedTypeModelAreCounted()
    {
        // The one thing this mode cannot do, as a number. It is asserted so
        // that a change in the pinned model moves it visibly rather than
        // quietly changing what the artifact tells a reader it lost.
        var schema = RecordSchemaDerivation.Derive(ReflectedRecordTypeSource.FromPinnedTypeModel());

        Assert.Equal(2095, SchemaIr.ReferenceFieldCount(schema));
    }

    [Fact]
    public void NothingInThePinnedTypeModelFailsToDerive()
    {
        var schema = RecordSchemaDerivation.Derive(ReflectedRecordTypeSource.FromPinnedTypeModel());

        Assert.Empty(schema.Failures);
    }

    [Fact]
    public void TheSourceNamesWhatItReadForAProvenanceBlock()
    {
        var description = ReflectedRecordTypeSource.FromPinnedTypeModel().Description;

        Assert.Contains("8.20.0", description, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\", description, StringComparison.Ordinal);
        Assert.DoesNotContain("/home/", description, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldNamesComeFromTheTypeModelsAnnotationNotFromThePropertyName()
    {
        // The two disagree for almost every field in the pinned model, and it
        // is the annotation that matches how stored values are keyed. Reading
        // the property name instead would produce a schema that looks complete
        // and explains almost nothing.
        var schema = Derive(typeof(gamedataProbeAnnotated_Record));
        var fields = schema.Find("gamedataProbeAnnotated_Record")!.Fields;

        Assert.Contains("annotatedName", fields.Keys);
        Assert.DoesNotContain("DifferentPropertyName", fields.Keys);
        Assert.Equal("Float", fields["annotatedName"].StorageType);
    }

    [Fact]
    public void AnAnnotationWithNoNameFallsBackToThePropertyName()
    {
        var schema = Derive(typeof(gamedataProbeAnnotated_Record));

        Assert.Contains("Unnamed", schema.Find("gamedataProbeAnnotated_Record")!.Fields.Keys);
    }

    [Fact]
    public void APropertyWithNoAnnotationIsNotAField()
    {
        var schema = Derive(typeof(gamedataProbeAnnotated_Record));

        Assert.DoesNotContain("NotAField", schema.Find("gamedataProbeAnnotated_Record")!.Fields.Keys);
    }

    [Fact]
    public void AncestorsAreFollowedEvenWhenTheyWereNotListed()
    {
        // Only the record type is handed in. Its base carries a field, and a
        // source that looked only at what it was given would lose that field
        // without saying anything.
        var schema = Derive(typeof(gamedataProbeDerived_Record));

        Assert.Contains("inherited", schema.Find("gamedataProbeDerived_Record")!.Fields.Keys);
    }

    [Fact]
    public void ATypeThatIsNotARecordTypeIsCarriedButNotCountedAsOne()
    {
        var schema = Derive(typeof(gamedataProbeDerived_Record));

        Assert.Equal(new[] { "gamedataProbeDerived_Record" }, schema.RecordTypeNames);
        Assert.Contains(schema.AllTypes(), type => type.Name == nameof(ProbeBaseClass) && !type.IsRecordType);
    }

    [Fact]
    public void TwoTypesOfTheSameNameAreReportedRatherThanSilentlyMerged()
    {
        var source = new ReflectedRecordTypeSource(
            new[] { typeof(gamedataProbeClash_Record), typeof(Elsewhere.gamedataProbeClash_Record) },
            "two types that share a name");

        var failure = Assert.Single(RecordSchemaDerivation.Derive(source).Failures);

        Assert.Contains("both named", failure.Reason, StringComparison.Ordinal);
    }

    private static RecordSchema Derive(params Type[] types) =>
        RecordSchemaDerivation.Derive(new ReflectedRecordTypeSource(types, "types written for this test"));
}
