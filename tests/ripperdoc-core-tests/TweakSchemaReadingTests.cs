using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class TweakSchemaReadingTests
{
    private const string WidgetType = "gamedataProbeWidget_Record";

    // A schema with one record type carrying two fields, derived through the
    // real transform from a reading built here rather than from any install.
    private static RecordSchema Schema() => RecordSchemaDerivation.Derive(
        new RecordTypeSourceReading(
            [
                new RecordTypeShape(
                    WidgetType,
                    null,
                    true,
                    [new RecordFieldShape("price", "Int32"), new RecordFieldShape("label", "String")]),
            ],
            []),
        "a schema built for this check");

    private static IReadOnlyList<TweakDocument> Documents(params (string Path, string Content)[] files)
    {
        using var layer = SyntheticTweakLayer.Of(files);
        var enumerated = layer.Enumerate();

        return TweakFileReader.ReadLayer(enumerated, layer.Root);
    }

    [Theory]
    [InlineData("gamedataProbeWidget_Record")]
    [InlineData("ProbeWidget_Record")]
    [InlineData("ProbeWidget")]
    [InlineData("gamedataProbeWidget")]
    public void EverySpellingOfATypeNameResolvesToTheOneTheSchemaKnows(string spelled)
    {
        Assert.Equal(WidgetType, RecordTypeNaming.Canonicalise(spelled));

        var documents = Documents(("alpha\\a.yaml", $"Probe.one:\n  $type: {spelled}\n  price: 1\n"));
        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        Assert.Empty(reading.TypesTheSchemaLacks);
        Assert.Equal(1, reading.PropertiesChecked);
    }

    [Fact]
    public void ANameShapedLikeATypeButNamingNoneIsReportedRatherThanAccepted()
    {
        // Shape is not a validity test. A well-formed name can be invented, and
        // one that is not a type name at all comes out of canonicalisation
        // dressed as one - so both are resolved against the type set.
        var documents = Documents((
            "alpha\\a.yaml",
            "Probe.one:\n  $type: gamedataInventedThing_Record\n  price: 1\nProbe.two:\n  $type: Items.NotAType\n  price: 2\n"));

        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        Assert.Equal(
            new[] { "gamedataInventedThing_Record", "gamedataItems.NotAType_Record" },
            reading.TypesTheSchemaLacks.Select(unknown => unknown.Canonical).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "Probe.one", "Probe.two" },
            reading.RecordsWithNoResolvedType);
    }

    [Fact]
    public void ACloneTakesItsSourcesTypeEvenThroughAChain()
    {
        var documents = Documents(
            ("alpha\\a.yaml", $"Probe.one:\n  $type: {WidgetType}\n  price: 1\n"),
            ("beta\\b.yaml", "Probe.two:\n  $base: Probe.one\n  price: 2\n"),
            ("gamma\\c.yaml", "Probe.three:\n  $base: Probe.two\n  price: 3\n"));

        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        Assert.Equal(3, reading.PropertiesChecked);
        Assert.Empty(reading.RecordsWithNoResolvedType);
    }

    [Fact]
    public void ACloneOfSomethingDeclaredInALaterFileStillResolves()
    {
        // The framework resolves the whole changeset before committing any of
        // it, so a one-pass read in file order would resolve fewer types than
        // the game does and under-report what could be checked.
        var documents = Documents(
            ("alpha\\a.yaml", "Probe.two:\n  $base: Probe.one\n  price: 2\n"),
            ("beta\\b.yaml", $"Probe.one:\n  $type: {WidgetType}\n  price: 1\n"));

        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        Assert.Equal(2, reading.PropertiesChecked);
        Assert.Empty(reading.RecordsWithNoResolvedType);
    }

    [Fact]
    public void ACloneChainThatLoopsIsRefusedRatherThanFollowedForever()
    {
        var documents = Documents(
            ("alpha\\a.yaml", "Probe.one:\n  $base: Probe.two\n  price: 1\n"),
            ("beta\\b.yaml", "Probe.two:\n  $base: Probe.one\n  price: 2\n"));

        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        Assert.Equal(0, reading.PropertiesChecked);
        Assert.Equal(new[] { "Probe.one", "Probe.two" }, reading.RecordsWithNoResolvedType);
    }

    [Fact]
    public void ARecordTheLayerNeverDeclaresIsNamedAsUnresolvedRatherThanTreatedAsClean()
    {
        var documents = Documents(("alpha\\a.yaml", "Probe.shipped:\n  price: 5\n"));

        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        Assert.Equal(0, reading.PropertiesChecked);
        Assert.Equal("Probe.shipped", Assert.Single(reading.RecordsWithNoResolvedType));
    }

    [Fact]
    public void APropertyNeitherSourceHasIsNamedWithItsFileAndLine()
    {
        var documents = Documents((
            "alpha\\a.yaml",
            $"Probe.one:\n  $type: {WidgetType}\n  price: 1\n  invented: 2\n"));

        var extraFlats = ExtraFlatsDeclaring(("gamedataOtherThing_Record", "invented"));
        var reading = TweakSchemaReading.Of(documents, Schema(), extraFlats);

        Assert.True(reading.UnknownPropertiesWereCounted);
        var unknown = Assert.Single(reading.PropertiesTheSchemaLacks);
        Assert.Equal("invented", unknown.PropertyName);
        Assert.Equal("Probe.one", unknown.RecordName);
        Assert.Equal(WidgetType, unknown.TypeName);
        Assert.Equal("alpha\\a.yaml", unknown.RelativePath);
        Assert.Equal(4, unknown.Line);
    }

    [Fact]
    public void APropertyTheFrameworkAddsIsNotReportedAsOneTheSchemaLacks()
    {
        var documents = Documents((
            "alpha\\a.yaml",
            $"Probe.one:\n  $type: {WidgetType}\n  price: 1\n  addedByTheFramework: 2\n"));

        var extraFlats = ExtraFlatsDeclaring((WidgetType, "addedByTheFramework"));
        var reading = TweakSchemaReading.Of(documents, Schema(), extraFlats);

        Assert.Equal(2, reading.PropertiesChecked);
        Assert.Empty(reading.PropertiesTheSchemaLacks);
    }

    [Fact]
    public void WithoutTheFrameworksMetadataTheCountIsWithheldRatherThanReportedWrong()
    {
        var documents = Documents((
            "alpha\\a.yaml",
            $"Probe.one:\n  $type: {WidgetType}\n  price: 1\n  addedByTheFramework: 2\n"));

        var reading = TweakSchemaReading.Of(documents, Schema(), TweakExtraFlats.None);

        // The property is not in the schema, and without the framework's own
        // metadata there is no way to tell that from a property that does not
        // exist. The reading says it did not count rather than counting wrong.
        Assert.False(reading.UnknownPropertiesWereCounted);
        Assert.Empty(reading.PropertiesTheSchemaLacks);
        Assert.Equal(2, reading.PropertiesChecked);
        Assert.Contains("no framework metadata", reading.PropertySourceDescription, StringComparison.Ordinal);
    }

    private static TweakExtraFlats ExtraFlatsDeclaring(params (string TypeName, string Property)[] additions)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write((ulong)additions.Length);

            foreach (var (typeName, property) in additions)
            {
                writer.Write(TweakExtraFlats.NameHash(typeName));
                writer.Write(1UL);
                writer.Write((byte)property.Length);
                writer.Write(System.Text.Encoding.ASCII.GetBytes(property));
                writer.Write(0UL);
                writer.Write(0UL);
            }
        }

        var path = Path.Combine(Path.GetTempPath(), "extra-" + Guid.NewGuid().ToString("N")[..12] + ".dat");
        File.WriteAllBytes(path, stream.ToArray());

        try
        {
            return TweakExtraFlats.OpenReadOnly(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
