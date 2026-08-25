using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Each failure kind says what it knows, and nothing it does not.
/// </summary>
/// <remarks>
/// The channel these replace carried four different causes on one sentence, so
/// the sentence had to guess and guessed wrong: a truncated download was
/// reported as a permissions problem, because that is the exception the pinned
/// library happens to raise for it. What is checked here is per kind, because
/// the whole point of the kind is that its message is derived from it.
/// </remarks>
[Collection(ResolverCollection.Name)]
public sealed class ArchiveFailureKindTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ripperdoc-failure-tests-" + Guid.NewGuid().ToString("N"));

    public ArchiveFailureKindTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a check over.
        }
    }

    [Fact]
    public void AnErrorRaisedWhileNamingEntriesIsThisEnginesFailureAndNotTheArchives()
    {
        SyntheticArchive.Write(_directory, "rdp_intact.archive", @"base\rdp\a.json");

        var reader = new ArchiveInventoryReader(
            new ArchiveOnlyResourceNames(),
            _ => throw new InvalidOperationException("the resolver came apart"));

        var thrown = Assert.Throws<ArchiveReadException>(() => reader.Read(_directory));

        Assert.Equal(ArchiveFailureKind.NamingFailed, thrown.Kind);

        // The archive was read. Saying it could not be would send a reader to
        // inspect a file that is intact - which is the row this used to become.
        Assert.DoesNotContain(
            "could not read this archive's index", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("the resolver came apart", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("rdp_intact.archive", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APathThatIsAFileIsRefusedAsAFileRatherThanAsUnresolvable()
    {
        var file = Path.Combine(_directory, "not-a-directory.txt");
        File.WriteAllText(file, "this path resolves; it is simply not a directory");

        var thrown = Assert.Throws<ArchiveReadException>(
            () => new ArchiveInventoryReader(new ArchiveOnlyResourceNames()).Read(file));

        Assert.Equal(ArchiveFailureKind.NotADirectory, thrown.Kind);
        Assert.DoesNotContain("does not resolve", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(file, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AListingThatFailsIsAnnouncedByKindInsteadOfEscapingAsItself()
    {
        // Driven at the listing rather than through a read, because the failure
        // it announces is a directory the caller cannot list - reachable on a
        // real machine and not producible on a runner without changing access
        // control. What is held here is that the route exists and classifies.
        var missing = Path.Combine(_directory, "no-such-directory");

        var thrown = Assert.Throws<ArchiveReadException>(
            () => ArchiveInventoryReader.EnumerateNestedArchives(missing));

        Assert.Equal(ArchiveFailureKind.Unclassified, thrown.Kind);
        Assert.IsAssignableFrom<DirectoryNotFoundException>(thrown.InnerException);
    }

    [Fact]
    public void OnlyADeniedListingIsClassifiedAsADenialAndItKeepsTheListingsApart()
    {
        // Both arms, because the second is what stops every unrecognised
        // failure from being reported as a permissions problem - the exact
        // misdirection this model replaced.
        Assert.Equal(
            ArchiveFailureKind.InaccessibleModDirectory,
            ArchiveFailure.Classify(
                new UnauthorizedAccessException(), ArchiveFailureKind.InaccessibleModDirectory));

        Assert.Equal(
            ArchiveFailureKind.InaccessibleSubdirectory,
            ArchiveFailure.Classify(
                new UnauthorizedAccessException(), ArchiveFailureKind.InaccessibleSubdirectory));

        Assert.Equal(
            ArchiveFailureKind.Unclassified,
            ArchiveFailure.Classify(new IOException(), ArchiveFailureKind.InaccessibleSubdirectory));
    }

    [Fact]
    public void AnInaccessibleModDirectorySaysThePathItselfWasRefused()
    {
        var message = ArchiveFailure.Describe(
            ArchiveFailureKind.InaccessibleModDirectory,
            "somewhere",
            "it raised UnauthorizedAccessException: denied");

        Assert.Contains("The mod directory 'somewhere' could not be listed", message, StringComparison.Ordinal);
        Assert.DoesNotContain("A directory under", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUnclassifiedKindCarriesTheUnderlyingErrorAndAssertsNoCause()
    {
        var message = ArchiveFailure.Describe(
            ArchiveFailureKind.Unclassified, "somewhere", "it raised IOException: the disk went away");

        Assert.Contains("the disk went away", message, StringComparison.Ordinal);
        Assert.Contains("claims nothing about the cause", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInaccessibleSubdirectorySaysTheEnumerationStoppedRatherThanOmitting()
    {
        var message = ArchiveFailure.Describe(
            ArchiveFailureKind.InaccessibleSubdirectory,
            "somewhere",
            "it raised UnauthorizedAccessException: denied");

        // The sentence directs what happens next, so what it directs is
        // measured: a caller must not read this as a completed enumeration.
        Assert.Contains("stops here", message, StringComparison.Ordinal);
        Assert.Contains("silently omits", message, StringComparison.Ordinal);
    }
}
