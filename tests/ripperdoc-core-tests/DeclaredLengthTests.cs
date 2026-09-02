using System.Text.RegularExpressions;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The one place a declared length is held against the bytes there are, and
/// the check that it is the one place.
/// </summary>
public sealed partial class DeclaredLengthTests
{
    private const string Primitive = "DeclaredLength.cs";

    private static readonly byte[] Sixteen = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];

    /// <summary>
    /// A length that wraps the sum negative in an int is refused by name, not
    /// let through to the slice.
    /// </summary>
    [Fact]
    public void ALengthThatWrapsTheSumIsRefusedByName()
    {
        var refusal = Assert.Throws<StateReadException>(() => Next(8, int.MaxValue - 4));

        Assert.Contains(
            "a fixture names a key of 2147483643 bytes at byte 8, which is not within the 16 bytes "
            + "there are",
            refusal.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ALengthRunningPastTheEndIsRefusedByName()
    {
        var refusal = Assert.Throws<StateReadException>(() => At(10, 7));

        Assert.Contains(
            "a key of 7 bytes at byte 10, which is not within the 16 bytes there are",
            refusal.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void ANegativeOffsetOrLengthIsRefusedByName(int at, long length)
    {
        var refusal = Assert.Throws<StateReadException>(() => At(at, length));

        // The whole sentence, because a fragment cannot tell a true sentence
        // from one whose second half describes an overrun that did not happen.
        Assert.Equal(
            $"a fixture names a key of {length} bytes at byte {at}, which is not within the 16 "
            + "bytes there are. The file is truncated, or is not the format this reader models.",
            refusal.Message);
    }

    /// <summary>
    /// A length that ends exactly at the end reads, so the refusals above are
    /// about the overrun and not about the edge.
    /// </summary>
    [Fact]
    public void ALengthEndingExactlyAtTheEndReads()
    {
        var at = 12;
        var taken = DeclaredLength.Next(Sixteen.AsSpan(), ref at, 4, "a fixture", "a key");

        Assert.Equal(16, at);
        Assert.Equal([12, 13, 14, 15], taken.ToArray());
        Assert.Equal([3, 4], DeclaredLength.Memory(Sixteen, 3, 2, "a fixture", "a key").ToArray());
        Assert.Equal(0, DeclaredLength.At(Sixteen, 16, 0, "a fixture", "a key").Length);
    }

    /// <summary>
    /// No file in the reader slices a buffer by a computed length except the
    /// primitive's own, so a site added later cannot carry the wrap without
    /// failing here.
    /// </summary>
    /// <remarks>
    /// An identity check over the source rather than a behavioural one: a
    /// behavioural arm per site passes on the day a new site arrives without
    /// one. Each file is scanned whole, so a call whose arguments begin on the
    /// next line is the same call. The forms held are a slice call with
    /// arguments, a span, memory or segment constructed over a buffer, a copy
    /// naming an offset, and a range with a computed endpoint; a range whose
    /// endpoints are literals, or count a literal from the end, names a fixed
    /// trailer rather than a declared length.
    /// </remarks>
    [Fact]
    public void EverySliceByAComputedLengthInTheReaderIsThePrimitives()
    {
        var sources = Directory.GetFiles(
            Path.Combine(AppContext.BaseDirectory, "ManagerState"), "*.cs");

        Assert.Contains(sources, path => Path.GetFileName(path) == Primitive);
        Assert.Contains(sources, path => Path.GetFileName(path) == "WriteBatch.cs");

        var offending = new List<string>();

        foreach (var path in sources.Where(path => Path.GetFileName(path) != Primitive))
        {
            var text = File.ReadAllText(path);

            offending.AddRange(Slices(text).Select(
                slice => $"{Path.GetFileName(path)}:{Line(text, slice.Index)}: {slice.Value}"));
        }

        Assert.Empty(offending);
    }

    /// <summary>
    /// The primitive's own file makes exactly the slices its members exist to
    /// make, each after the check - so a raw slice helper added beside them
    /// fails here rather than being exempted with the file.
    /// </summary>
    [Fact]
    public void ThePrimitiveSlicesOnlyAfterItsOwnCheck()
    {
        var text = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "ManagerState", Primitive));
        var slices = Slices(text).ToList();

        Assert.Equal(3, slices.Count);

        foreach (var slice in slices)
        {
            var check = text.LastIndexOf(
                "Check(buffer.Length, at, length, what, of);", slice.Index, StringComparison.Ordinal);

            Assert.True(check >= 0, $"no check before {slice.Value} at {Line(text, slice.Index)}");

            // Nothing closes between the two, so the slice is in the body the
            // check guards and not in a later member of its own.
            Assert.DoesNotContain("}", text.Substring(check, slice.Index - check), StringComparison.Ordinal);
        }
    }

    private static void At(int at, long length) =>
        DeclaredLength.At((ReadOnlySpan<byte>)Sixteen, at, length, "a fixture", "a key");

    private static void Next(int at, long length) =>
        DeclaredLength.Next((ReadOnlySpan<byte>)Sixteen, ref at, length, "a fixture", "a key");

    private static IEnumerable<Match> Slices(string text) =>
        SliceCall().Matches(text)
            .Concat(BufferView().Matches(text))
            .Concat(CopyByOffset().Matches(text))
            .Concat(RangeIndex().Matches(text).Where(Computed))
            .OrderBy(match => match.Index);

    private static int Line(string text, int index) =>
        text.Take(index).Count(c => c == '\n') + 1;

    private static bool Computed(Match range) =>
        !Fixed(range.Groups["from"].Value) || !Fixed(range.Groups["to"].Value);

    private static bool Fixed(string endpoint) =>
        endpoint.Length == 0
        || endpoint.All(char.IsDigit)
        || (endpoint.Length > 1 && endpoint[0] == '^' && endpoint[1..].All(char.IsDigit));

    [GeneratedRegex(@"\.(Slice|AsSpan|AsMemory)\s*\(\s*[^)\s]")]
    private static partial Regex SliceCall();

    // A span, memory or segment constructed over a buffer with arguments,
    // whether the type is spelled or left to the target; and the marshal
    // that makes one from anything. A bare "new (" whose parenthesis is
    // followed by "[" is an array of tuples, not a target-typed view.
    [GeneratedRegex(
        @"\bnew\s*(?:\((?![^()]*\)\s*\[)"
        + @"|(?:ReadOnlySpan|Span|ReadOnlyMemory|Memory|ArraySegment)\s*<[^>]*>\s*\()\s*[^)\s]"
        + @"|\bMemoryMarshal\b")]
    private static partial Regex BufferView();

    // A copy naming an offset: the static copies, and a CopyTo with more than
    // a destination - its first argument balanced to the top-level comma.
    [GeneratedRegex(
        @"\b(?:Array\s*\.\s*Copy|Buffer\s*\.\s*(?:BlockCopy|MemoryCopy))\s*\("
        + @"|\.CopyTo\s*\((?:[^(),]|\((?:[^()]|\([^()]*\))*\))*,")]
    private static partial Regex CopyByOffset();

    [GeneratedRegex(@"[\w\)\]]\[\s*(?<from>[^\[\]]*?)\.\.(?<to>[^\[\]]*?)\s*\]")]
    private static partial Regex RangeIndex();
}
