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
    public void ASubdirectoryThatCannotBeListedIsReportedAsASubdirectory()
    {
        SyntheticArchive.Write(_directory, "rdp_top.archive", @"base\rdp\a.json");

        var nested = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nested);
        Deny(nested);

        var thrown = Assert.Throws<ArchiveReadException>(
            () => new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(_directory));

        Assert.Equal(ArchiveFailureKind.InaccessibleSubdirectory, thrown.Kind);
        Assert.Contains(
            $"A directory under '{_directory}' could not be listed",
            thrown.Message,
            StringComparison.Ordinal);
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
