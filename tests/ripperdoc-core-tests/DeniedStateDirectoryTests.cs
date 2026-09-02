using System.Diagnostics;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The state reader against a directory the running user may read and not
/// write, and against one whose pointer it is refused.
/// </summary>
/// <remarks>
/// <para>
/// The reader claims it cannot write to a manager's state. A directory that
/// refuses writes is what turns that claim into something a check can fail: a
/// reader that created, truncated or appended to anything would be refused
/// here, and reads green only because it does none of those.
/// </para>
/// <para>
/// The <c>icacls</c> plumbing is this file's own rather than shared with the
/// sibling tier. Two sites is a copy; a third would be the point to extract
/// one.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.DeniedDirectory)]
public sealed class DeniedStateDirectoryTests : IDisposable
{
    private static readonly string[] Prefixes = ["persistent###mods###"];

    private readonly SyntheticStateDatabase scratch = new();
    private readonly List<(string Path, string Right)> denied = [];

    private string root = string.Empty;

    public DeniedStateDirectoryTests() =>
        scratch.Table(("persistent###mods###a", "\"one\""), ("persistent###mods###b", "\"two\""));

    public void Dispose()
    {
        // The denials come off before the tree does, or the tree stays.
        foreach (var (path, _) in denied)
        {
            Icacls(path, "/remove:d", Environment.UserName);
        }

        scratch.Dispose();
    }

    /// <summary>
    /// A state directory the process cannot write to reads green.
    /// </summary>
    [Fact]
    public void AStateDirectoryThatCannotBeWrittenToIsReadWhole()
    {
        root = scratch.Write();
        Deny(root, "(W)");

        var state = StateDatabase.In(root, Prefixes)!;

        Assert.Equal(2, state.Values.Count);
        Assert.Equal("\"one\"", state.Text("persistent###mods###a"));
    }

    /// <summary>
    /// A pointer that is there and cannot be read is refused by name rather
    /// than read as a directory holding no database.
    /// </summary>
    /// <remarks>
    /// The two states differ by everything downstream. Read as absence, this
    /// directory yields no wanted set, and every mod the manager asked for
    /// would be reported as one nothing can say anything about - on the
    /// strength of a permission, not of what the manager holds.
    /// </remarks>
    [Fact]
    public void APointerThatCannotBeReadIsRefusedRatherThanTakenForAbsent()
    {
        root = scratch.Write();
        var pointer = Path.Combine(root, StateVersion.PointerName);
        Deny(pointer, "(R)");

        var refusal = Assert.Throws<StateReadException>(() => StateDatabase.In(root, Prefixes));

        Assert.Contains(
            $"'{StateVersion.PointerName}' in '{root}' is there and could not be read",
            refusal.Message,
            StringComparison.Ordinal);
        Assert.Contains("may read it", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The manifest the pointer names, there and unreadable, is refused by name
    /// rather than taken for a directory holding no state.
    /// </summary>
    [Fact]
    public void AManifestThatCannotBeReadIsRefusedRatherThanTakenForAbsent()
    {
        root = scratch.Write();
        var named = File.ReadAllText(Path.Combine(root, StateVersion.PointerName)).Trim();
        Deny(Path.Combine(root, named), "(R)");

        var refusal = Assert.Throws<StateReadException>(() => StateDatabase.In(root, Prefixes));

        Assert.Contains(
            $"names '{named}'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(
            "there and could not be read", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A file the manifest names, there and unreadable, is refused by name
    /// rather than read past as though what it held were absent.
    /// </summary>
    [Fact]
    public void AFileTheManifestNamesThatCannotBeReadIsRefusedRatherThanReadPast()
    {
        root = scratch.Write();
        var table = Directory.EnumerateFiles(root, "*.ldb").Single();
        Deny(table, "(R)");

        var refusal = Assert.Throws<StateReadException>(() => StateDatabase.In(root, Prefixes));

        Assert.Contains(
            Path.GetFileName(table), refusal.Message, StringComparison.Ordinal);
        Assert.Contains(
            "there and could not be read", refusal.Message, StringComparison.Ordinal);
    }

    /// <remarks>
    /// The refusal is confirmed rather than assumed: a process holding a
    /// privilege that walks through it would leave these checks asserting
    /// nothing.
    /// </remarks>
    private void Deny(string path, string right)
    {
        Icacls(path, "/deny", $"{Environment.UserName}:{right}");
        denied.Add((path, right));

        if (right == "(W)")
        {
            Assert.Throws<UnauthorizedAccessException>(
                () => File.WriteAllText(Path.Combine(path, "a-write-that-must-not-work"), "x"));
        }
        else
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllBytes(path));
        }
    }

    private static void Icacls(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo("icacls") { RedirectStandardOutput = true };
        start.ArgumentList.Add(path);

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        process.WaitForExit();
    }
}
