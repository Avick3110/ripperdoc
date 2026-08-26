using System.Diagnostics;
using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// A listing the running user is refused, and what the read says it was.
/// </summary>
/// <remarks>
/// The mod directory and a directory under it are refused the same way and are
/// different facts, so each arm is driven through a real denial rather than
/// through the classifier alone.
/// </remarks>
[Trait(TierTrait.Name, TierTrait.DeniedDirectory)]
[Collection(ResolverCollection.Name)]
public sealed class DeniedListingTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ripperdoc-denied-tests-" + Guid.NewGuid().ToString("N"));

    private readonly List<string> _denied = [];

    public DeniedListingTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        // The denials come off before the tree does, or the tree stays.
        foreach (var path in _denied)
        {
            Icacls(path, "/remove:d", Environment.UserName);
        }

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp directory is not worth failing a check over.
        }
    }

    [Fact]
    public void AModDirectoryThatCannotBeListedIsReportedAsItselfAndNotAsSomethingUnderIt()
    {
        SyntheticArchive.Write(_directory, "rdp_one.archive", @"base\rdp\a.json");
        Deny(_directory);

        var thrown = Assert.Throws<ArchiveReadException>(
            () => new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(_directory));

        Assert.Equal(ArchiveFailureKind.InaccessibleModDirectory, thrown.Kind);
        Assert.Contains(
            $"The mod directory '{_directory}' could not be listed",
            thrown.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("A directory under", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASubdirectoryThatCannotBeListedIsRecordedAndTheArchivesAreStillReported()
    {
        SyntheticArchive.Write(_directory, "rdp_top.archive", @"base\rdp\a.json");

        var nested = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nested);
        Deny(nested);

        var inventory = new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(_directory);

        // The half a throw used to take down: the mod directory's own archives,
        // read and complete, beside the list that could not be taken.
        Assert.Equal(1, inventory.ArchiveCount);
        Assert.Single(Assert.Single(inventory.Archives).Entries);
        Assert.Empty(inventory.NestedArchivePaths);

        Assert.Equal(ArchiveFailureKind.InaccessibleSubdirectory, inventory.NestedListingFailureKind);
        Assert.Contains(
            $"A directory under '{_directory}' could not be listed",
            inventory.NestedListingFailure!,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A list file the caller cannot read stops the order rather than becoming
    /// a directory without one.
    /// </summary>
    /// <remarks>
    /// The two states differ by a whole branch of the precedence law. Read as
    /// absent, this directory would order every archive by file name and hand
    /// back a confident winner for every contest, wrong wherever the list
    /// disagreed - which is the failure this layer exists to remove, arrived at
    /// by the layer itself.
    /// </remarks>
    [Fact]
    public void AListFileThatCannotBeReadIsRefusedRatherThanTakenForAbsent()
    {
        SyntheticArchive.Write(_directory, "rdp_one.archive", @"base\rdp\a.json");

        var list = Path.Combine(_directory, Modlist.FileName);
        File.WriteAllLines(list, ["rdp_one.archive"]);
        DenyFile(list);

        var thrown = Assert.Throws<ArchiveReadException>(() => Modlist.Read(_directory));

        Assert.Equal(ArchiveFailureKind.UnreadableModlist, thrown.Kind);
        Assert.Contains("no order is reported", thrown.Message, StringComparison.Ordinal);
        Assert.False(Modlist.Absent.IsPresent);
    }

    /// <summary>
    /// Refuses the running user this file, and confirms the refusal took.
    /// </summary>
    /// <inheritdoc cref="Deny" path="/remarks" />
    private void DenyFile(string path)
    {
        Icacls(path, "/deny", $"{Environment.UserName}:(R)");
        _denied.Add(path);

        Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllLines(path));
    }

    /// <summary>
    /// Refuses the running user this directory, and confirms the refusal took.
    /// </summary>
    /// <remarks>
    /// A process holding a privilege that walks through the refusal would leave
    /// these checks asserting nothing, so the precondition is measured rather
    /// than assumed.
    /// </remarks>
    private void Deny(string path)
    {
        Icacls(path, "/deny", $"{Environment.UserName}:(RX)");
        _denied.Add(path);

        Assert.Throws<UnauthorizedAccessException>(
            () => Directory.EnumerateFileSystemEntries(path).ToList());
    }

    private static void Icacls(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo("icacls.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add(path);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        process?.WaitForExit();
    }
}
