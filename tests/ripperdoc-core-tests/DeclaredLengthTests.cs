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
            "a fixture names a key of 2147483643 bytes at byte 8, which runs past the end of the "
            + "16 bytes there are",
            refusal.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ALengthRunningPastTheEndIsRefusedByName()
    {
        var refusal = Assert.Throws<StateReadException>(() => At(10, 7));

        Assert.Contains("a key of 7 bytes at byte 10", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("runs past the end", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void ANegativeOffsetOrLengthIsRefusedByName(int at, long length)
    {
        var refusal = Assert.Throws<StateReadException>(() => At(at, length));

        Assert.Contains($"of {length} bytes at byte {at}", refusal.Message, StringComparison.Ordinal);
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
    /// one. The forms held are a slice call with arguments and a range with a
    /// computed endpoint; a range whose endpoints are literals or counted from
    /// the end names a fixed trailer, not a declared length.
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
            var lines = File.ReadAllLines(path);

            for (var i = 0; i < lines.Length; i++)
            {
                if (SliceCall().IsMatch(lines[i]) || RangeIndex().Matches(lines[i]).Any(Computed))
                {
                    offending.Add($"{Path.GetFileName(path)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.Empty(offending);
    }

    private static void At(int at, long length) =>
        DeclaredLength.At((ReadOnlySpan<byte>)Sixteen, at, length, "a fixture", "a key");

    private static void Next(int at, long length) =>
        DeclaredLength.Next((ReadOnlySpan<byte>)Sixteen, ref at, length, "a fixture", "a key");

    private static bool Computed(Match range) =>
        !Fixed(range.Groups["from"].Value) || !Fixed(range.Groups["to"].Value);

    private static bool Fixed(string endpoint) =>
        endpoint.Length == 0 || endpoint.StartsWith('^') || endpoint.All(char.IsDigit);

    [GeneratedRegex(@"\.(Slice|AsSpan|AsMemory)\(\s*[^)\s]")]
    private static partial Regex SliceCall();

    [GeneratedRegex(@"[\w\)\]]\[\s*(?<from>[^\[\]]*?)\.\.(?<to>[^\[\]]*?)\s*\]")]
    private static partial Regex RangeIndex();
}
