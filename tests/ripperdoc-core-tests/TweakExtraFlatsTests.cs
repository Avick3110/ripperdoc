using System.Text;
using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class TweakExtraFlatsTests
{
    // The metadata file's own layout, written here rather than copied from
    // anyone's install: a count of record types, then per type its name hash, a
    // count of additions, and per addition a length-prefixed name followed by
    // its declared and referenced type hashes.
    private static byte[] Metadata(params (string TypeName, string[] Properties)[] types)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write((ulong)types.Length);

        foreach (var (typeName, properties) in types)
        {
            writer.Write(TweakExtraFlats.NameHash(typeName));
            writer.Write((ulong)properties.Length);

            foreach (var property in properties)
            {
                writer.Write((byte)property.Length);
                writer.Write(Encoding.ASCII.GetBytes(property));
                writer.Write(0UL);
                writer.Write(0UL);
            }
        }

        writer.Flush();

        return stream.ToArray();
    }

    private static T WithFile<T>(byte[] bytes, Func<string, T> use)
    {
        var path = Path.Combine(Path.GetTempPath(), "extra-flats-" + Guid.NewGuid().ToString("N")[..12] + ".dat");
        File.WriteAllBytes(path, bytes);

        try
        {
            return use(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void APropertyTheFrameworkAddsIsFoundUnderTheTypeItWasDeclaredOn()
    {
        var extraFlats = WithFile(
            Metadata(("gamedataProbeWidget_Record", ["addedOne", "addedTwo"])),
            TweakExtraFlats.OpenReadOnly);

        Assert.True(extraFlats.IsPresent);
        Assert.Equal(1, extraFlats.TypeCount);
        Assert.Equal(2, extraFlats.PropertyCount);
        Assert.True(extraFlats.Declares("gamedataProbeWidget_Record", "addedOne"));
        Assert.True(extraFlats.Declares("gamedataProbeWidget_Record", "addedTwo"));
        Assert.False(extraFlats.Declares("gamedataProbeWidget_Record", "notAdded"));
        Assert.False(extraFlats.Declares("gamedataOtherThing_Record", "addedOne"));
    }

    [Fact]
    public void TheNameHashIsTheOneTheMetadataKeysTypesBy()
    {
        // The published vectors for this hash. If the arithmetic is wrong every
        // lookup misses and every added property reads as one the schema lacks,
        // which is a silent wrong answer rather than a failure.
        Assert.Equal(0xCBF29CE484222325UL, TweakExtraFlats.NameHash(string.Empty));
        Assert.Equal(0xAF63DC4C8601EC8CUL, TweakExtraFlats.NameHash("a"));
        Assert.Equal(0x85944171F73967E8UL, TweakExtraFlats.NameHash("foobar"));
    }

    [Fact]
    public void MetadataThatEndsEarlyIsRefusedByName()
    {
        var truncated = Metadata(("gamedataProbeWidget_Record", ["addedOne"]))[..12];

        var exception = WithFile(truncated, path =>
            Assert.Throws<InvalidDataException>(() => TweakExtraFlats.OpenReadOnly(path)));

        Assert.Contains("ends before the structure it declares does", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataWithBytesLeftOverIsRefusedRatherThanPartlyRead()
    {
        // A file that parses and leaves a tail was not understood. Accepted, it
        // would drop whatever the tail declared, and every property in it would
        // then be reported as one the schema lacks.
        var withTail = Metadata(("gamedataProbeWidget_Record", ["addedOne"])).Concat(new byte[] { 1, 2, 3 }).ToArray();

        var exception = WithFile(withTail, path =>
            Assert.Throws<InvalidDataException>(() => TweakExtraFlats.OpenReadOnly(path)));

        Assert.Contains("3 of", exception.Message, StringComparison.Ordinal);
        Assert.Contains("bytes remain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACountLargerThanTheFileCouldHoldIsRefusedRatherThanAllocatedOn()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(1UL);
            writer.Write(TweakExtraFlats.NameHash("gamedataProbeWidget_Record"));
            writer.Write(0x7FFF_FFFFUL);
        }

        var exception = WithFile(stream.ToArray(), path =>
            Assert.Throws<InvalidDataException>(() => TweakExtraFlats.OpenReadOnly(path)));

        Assert.Contains("bytes left to read them from", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeNamedTwiceContributesBothSetsRatherThanLosingOne()
    {
        // The framework appends each entry to whatever it already holds for a
        // record type. Replacing would drop additions the game accepts while
        // the count went on reporting them, so a property that works would be
        // reported as one the schema lacks.
        var extraFlats = WithFile(
            Metadata(
                ("gamedataProbeWidget_Record", ["first"]),
                ("gamedataProbeWidget_Record", ["second"])),
            TweakExtraFlats.OpenReadOnly);

        Assert.True(extraFlats.Declares("gamedataProbeWidget_Record", "first"));
        Assert.True(extraFlats.Declares("gamedataProbeWidget_Record", "second"));
        Assert.Equal(2, extraFlats.PropertyCount);
        Assert.Equal(1, extraFlats.TypeCount);
    }

    [Fact]
    public void APropertyNameIsMatchedExactlyRatherThanWithoutRegardToCase()
    {
        // Tweak property names are case-sensitive, so a lookup that ignored case
        // would accept a misspelling the game rejects and report nothing wrong.
        var extraFlats = WithFile(
            Metadata(("gamedataProbeWidget_Record", ["addedOne"])),
            TweakExtraFlats.OpenReadOnly);

        Assert.True(extraFlats.Declares("gamedataProbeWidget_Record", "addedOne"));
        Assert.False(extraFlats.Declares("gamedataProbeWidget_Record", "addedone"));
        Assert.False(extraFlats.Declares("gamedataprobewidget_Record", "addedOne"));
    }

    [Fact]
    public void MetadataThatIsNotThereFailsByName()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-extra-flats-" + Guid.NewGuid().ToString("N")[..12] + ".dat");

        Assert.Throws<FileNotFoundException>(() => TweakExtraFlats.OpenReadOnly(missing));
    }

    [Fact]
    public void TheAbsentSourceAddsNothingAndSaysThatIsWhatItIs()
    {
        Assert.False(TweakExtraFlats.None.IsPresent);
        Assert.Equal(0, TweakExtraFlats.None.PropertyCount);
        Assert.False(TweakExtraFlats.None.Declares("gamedataProbeWidget_Record", "addedOne"));
    }

    [Fact]
    public void MetadataDeclaringNothingIsReadableAndAddsNothing()
    {
        var extraFlats = WithFile(Metadata(), TweakExtraFlats.OpenReadOnly);

        Assert.False(extraFlats.IsPresent);
        Assert.Equal(0, extraFlats.TypeCount);
    }
    [Fact]
    public void AnAdditionWithNoNameIsReadRatherThanCalledATruncatedFile()
    {
        // The lower bound that guards the allocation used to count a name byte,
        // so a file declaring an addition with an empty name beat the bound and
        // was refused as ending early. It does not end early; it says something
        // odd, and the two are different reports to give a reader chasing a
        // property the game accepts and this tool does not.
        var extraFlats = WithFile(
            Metadata(("gamedataProbeWidget_Record", ["", "addedOne"])),
            TweakExtraFlats.OpenReadOnly);

        Assert.Equal(2, extraFlats.PropertyCount);
        Assert.True(extraFlats.Declares("gamedataProbeWidget_Record", "addedOne"));
    }

    [Fact]
    public void AFileThatEndsInsideItsLastAdditionSaysThatRatherThanCountingBackwards()
    {
        // The type hashes after a name are stepped over rather than read, so a
        // file ending inside them left the walk past the end of the buffer. The
        // tail then subtracted one from the other and reported a negative count
        // of bytes remaining - a file that stopped short, described as one with
        // something left over.
        var whole = Metadata(("gamedataProbeWidget_Record", ["addedOne"]));
        var truncated = whole[..^8];

        var exception = Assert.Throws<InvalidDataException>(
            () => WithFile(truncated, TweakExtraFlats.OpenReadOnly));

        Assert.Contains("ends before", exception.Message, StringComparison.Ordinal);
        // The temporary file's own name carries a hyphen, so what is asserted is
        // the wording rather than the absence of a minus sign: it ends early,
        // and nothing remains.
        Assert.DoesNotContain("remain", exception.Message, StringComparison.Ordinal);
    }

}
