using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class TweakInheritanceMapTests
{
    // The metadata file's own layout, written here rather than copied from
    // anyone's install: a count of source records, then per source its
    // identifier, a count of descendants, and those descendants' identifiers.
    private static byte[] Metadata(params (ulong Source, ulong[] Descendants)[] sources)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        writer.Write((ulong)sources.Length);

        foreach (var (source, descendants) in sources)
        {
            writer.Write(source);
            writer.Write((ulong)descendants.Length);

            foreach (var descendant in descendants)
            {
                writer.Write(descendant);
            }
        }

        writer.Flush();

        return stream.ToArray();
    }

    private static T WithFile<T>(byte[] bytes, Func<string, T> use)
    {
        var path = Path.Combine(Path.GetTempPath(), "inheritance-" + Guid.NewGuid().ToString("N")[..12] + ".dat");
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
    public void TheDescendantsOfASourceComeBackUnderThatSource()
    {
        var map = WithFile(Metadata((10UL, [20UL, 30UL])), TweakInheritanceMap.OpenReadOnly);

        Assert.True(map.WasRead);
        Assert.Equal(1, map.BaseCount);
        Assert.Equal(2, map.EdgeCount);
        Assert.True(map.IsSource(10UL));
        Assert.False(map.IsSource(20UL));
        Assert.Equal(new[] { 20UL, 30UL }, map.Descendants(10UL));
        Assert.Empty(map.Descendants(99UL));
    }

    [Fact]
    public void MetadataThatEndsEarlyIsRefusedByName()
    {
        var truncated = Metadata((10UL, [20UL]))[..12];

        var exception = WithFile(truncated, path =>
            Assert.Throws<InvalidDataException>(() => TweakInheritanceMap.OpenReadOnly(path)));

        Assert.Contains("ends before the structure it declares does", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataWithBytesLeftOverIsRefusedRatherThanPartlyRead()
    {
        var withTail = Metadata((10UL, [20UL])).Concat(new byte[] { 7, 7 }).ToArray();

        var exception = WithFile(withTail, path =>
            Assert.Throws<InvalidDataException>(() => TweakInheritanceMap.OpenReadOnly(path)));

        Assert.Contains("bytes remain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACountLargerThanTheFileCouldHoldIsRefusedRatherThanAllocatedOn()
    {
        // The count sizes an allocation and comes out of the file. Taken on
        // trust it raises something this method does not promise, or exhausts
        // memory before it can raise anything at all.
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(1UL);
            writer.Write(10UL);
            writer.Write(0x7FFF_FFFF_FFFFUL);
        }

        var exception = WithFile(stream.ToArray(), path =>
            Assert.Throws<InvalidDataException>(() => TweakInheritanceMap.OpenReadOnly(path)));

        Assert.Contains("bytes left to read them from", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASourceNamedTwiceContributesBothSetsRatherThanLosingOne()
    {
        var map = WithFile(Metadata((10UL, [20UL]), (10UL, [30UL])), TweakInheritanceMap.OpenReadOnly);

        Assert.Equal(new[] { 20UL, 30UL }, map.Descendants(10UL).OrderBy(id => id));
        Assert.Equal(2, map.EdgeCount);
        Assert.Contains("2 records cloned from 1 source", map.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataThatReadsCleanlyAndDeclaresNothingWasStillRead()
    {
        // "Read and found nothing" is not "nothing looked". A replay told the
        // second about a file it just read would announce that it did not
        // consult something it did.
        var map = WithFile(Metadata(), TweakInheritanceMap.OpenReadOnly);

        Assert.True(map.WasRead);
        Assert.Equal(0, map.EdgeCount);
        Assert.False(TweakInheritanceMap.None.WasRead);
    }

    [Fact]
    public void ARecordNamedWithNothingClonedFromItIsNotASource()
    {
        // The metadata can name a record and declare no copies of it. Read as
        // a source, it answers yes to whether anything was cloned from it and
        // is counted among the records that have copies - so a caller asking
        // where a write reaches beyond the record it names would be sent to a
        // record with nothing under it, and told so in a sentence.
        var map = WithFile(Metadata((10UL, []), (20UL, [30UL])), TweakInheritanceMap.OpenReadOnly);

        Assert.False(map.IsSource(10UL));
        Assert.Empty(map.Descendants(10UL));
        Assert.True(map.IsSource(20UL));
        Assert.Equal(1, map.BaseCount);
        Assert.Equal(1, map.EdgeCount);
        Assert.Contains("1 records cloned from 1 source", map.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void AMapBuiltDirectlyDropsASourceWithNoDescendantsToo()
    {
        // Two ways in, one answer out. A caller building the structure by hand
        // gets the same map the file reader would give it.
        var map = TweakInheritanceMap.Of(
            new Dictionary<ulong, IReadOnlyList<ulong>>
            {
                [10UL] = [],
                [20UL] = [30UL],
            },
            "a map built for this check");

        Assert.False(map.IsSource(10UL));
        Assert.True(map.IsSource(20UL));
        Assert.Equal(1, map.BaseCount);
        Assert.Equal(1, map.EdgeCount);
    }

    [Fact]
    public void MetadataThatIsNotThereFailsByName() =>
        Assert.Throws<FileNotFoundException>(() => TweakInheritanceMap.OpenReadOnly(
            Path.Combine(Path.GetTempPath(), "no-inheritance-" + Guid.NewGuid().ToString("N")[..12] + ".dat")));
}
