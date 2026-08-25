using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types.Pools;

namespace Ripperdoc.Core.Archive;

/// <summary>
/// Reads a mod directory's archives into an <see cref="ArchiveInventory" />.
/// </summary>
/// <remarks>
/// Each archive's index is read once, into the model. Nothing here re-opens an
/// archive to answer a later question, because a per-query open is the cost
/// shape that makes a whole-install read impossible at real scale.
/// <para>
/// Only the mod directory itself is enumerated. Archives inside subdirectories
/// are recorded separately rather than folded in - see
/// <see cref="ArchiveInventory.NestedArchivePaths" /> for why.
/// </para>
/// </remarks>
public sealed class ArchiveInventoryReader
{
    private const string ArchivePattern = "*.archive";

    private readonly IResourceNameSource _nameSource;
    private readonly Func<ulong, string?> _resolveName;

    /// <summary>
    /// Creates a reader that names resources with the given source.
    /// </summary>
    /// <param name="nameSource">
    /// Where names come from. <see cref="ArchiveOnlyResourceNames" /> is the
    /// posture that adds no dependency; a caller wanting the wider coverage
    /// passes the dictionary-backed source from the opt-in naming assembly.
    /// </param>
    public ArchiveInventoryReader(IResourceNameSource nameSource)
        : this(nameSource, ResolveName)
    {
    }

    /// <summary>
    /// The same reader, against a given name resolution.
    /// </summary>
    /// <remarks>
    /// Internal because resolution runs against a process-wide resolver that a
    /// check cannot make fail from outside, and the claim that a naming failure
    /// surfaces as this engine's rather than as an unreadable archive is one
    /// that has to be exercised rather than asserted. A seam that exists only
    /// for a check is worth naming as such.
    /// </remarks>
    internal ArchiveInventoryReader(IResourceNameSource nameSource, Func<ulong, string?> resolveName)
    {
        _nameSource = nameSource ?? throw new ArgumentNullException(nameof(nameSource));
        _resolveName = resolveName;
    }

    /// <summary>
    /// Reads every archive in <paramref name="modDirectory" />.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">
    /// Nothing is at the path. Announced rather than treated as an empty
    /// install, because "no archives" and "nowhere to look" are different
    /// answers and only one of them is about the mods.
    /// </exception>
    /// <exception cref="ArchiveReadException">
    /// The read could not be completed. <see cref="ArchiveReadException.Kind" />
    /// says which failure it was.
    /// </exception>
    /// <exception cref="ResourceNameSourceException">
    /// The naming source could not make its names available.
    /// </exception>
    public ArchiveInventory Read(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);

        if (!Directory.Exists(modDirectory))
        {
            // A path that is a file resolves, so it gets its own arm rather
            // than the message for a path that does not.
            if (File.Exists(modDirectory))
            {
                throw ArchiveFailure.Failure(ArchiveFailureKind.NotADirectory, modDirectory, inner: null);
            }

            throw new DirectoryNotFoundException(
                $"No directory at '{modDirectory}', so there is nothing to enumerate. " +
                "This is not an install with no mods - it is a path that does not resolve.");
        }

        // Before any archive is read: a source that cannot load then fails the
        // run instead of an under-named inventory being produced and reported
        // as a complete one.
        _nameSource.Prepare();

        var dictionaryLoaded = LoadedNameDictionary.IsLoaded();

        var reader = new ArchiveReader();
        var archives = new List<ArchiveContents>();

        foreach (var path in EnumerateArchives(modDirectory))
        {
            archives.Add(ReadOne(reader, path));
        }

        return new ArchiveInventory(
            archives,
            EnumerateNestedArchives(modDirectory),
            new InventoryProvenance(
                modDirectory,
                _nameSource.Description,
                // Observed rather than inferred from the source. A dictionary
                // loads into a process-wide resolver that cannot be unloaded,
                // so a read that installed none still sees one installed
                // earlier by anything else.
                dictionaryLoaded,
                ResourceLibraryVersion()));
    }

    /// <summary>
    /// The archives directly in the mod directory, in a fixed order.
    /// </summary>
    /// <remarks>
    /// Internal so that the failure route below can be exercised: a directory
    /// the caller cannot list is reachable on a real machine and not on a
    /// runner, and an announced failure that no check ever produces is the
    /// shape this project's proof discipline refuses.
    /// </remarks>
    internal static List<string> EnumerateArchives(string modDirectory) =>
        Listed(
            modDirectory,
            ArchiveFailureKind.InaccessibleModDirectory,
            () => Directory.EnumerateFiles(modDirectory, ArchivePattern, SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList());

    /// <summary>
    /// The archives in subdirectories of the mod directory, relative to it.
    /// </summary>
    /// <inheritdoc cref="EnumerateArchives" path="/remarks" />
    internal static List<string> EnumerateNestedArchives(string modDirectory) =>
        Listed(
            modDirectory,
            ArchiveFailureKind.InaccessibleSubdirectory,
            () => Directory.EnumerateDirectories(modDirectory)
                .SelectMany(directory =>
                    Directory.EnumerateFiles(directory, ArchivePattern, SearchOption.AllDirectories))
                .Select(path => Path.GetRelativePath(modDirectory, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList());

    /// <summary>
    /// Runs a listing, announcing a failure by kind instead of letting the
    /// file system's own exception escape unclassified.
    /// </summary>
    /// <param name="modDirectory">The directory the read was asked for.</param>
    /// <param name="denied">
    /// The kind to report a denial as. The two listings below are refused the
    /// same way and are different facts, so each names its own.
    /// </param>
    /// <param name="listing">The listing to run.</param>
    private static List<string> Listed(
        string modDirectory,
        ArchiveFailureKind denied,
        Func<List<string>> listing)
    {
        try
        {
            return listing();
        }
        catch (Exception exception)
        {
            throw ArchiveFailure.Failure(
                ArchiveFailure.Classify(exception, denied), modDirectory, exception);
        }
    }

    /// <summary>
    /// Reads one archive, or records why it could not be.
    /// </summary>
    /// <remarks>
    /// A failure to read the container is contained to this archive. A mod
    /// directory is other people's files, and a single truncated, empty or
    /// misnamed one is an ordinary condition rather than an exceptional one -
    /// letting it end the enumeration would lose every other archive's entries
    /// to one bad download.
    /// <para>
    /// The two things that can fail here are kept apart on purpose. Only the
    /// library's own call is caught into a row; what this engine does with a
    /// successfully read index is its own responsibility, and a fault there
    /// wearing the library's label would blame a file that is intact.
    /// </para>
    /// </remarks>
    private ArchiveContents ReadOne(ArchiveReader reader, string path)
    {
        var fileName = Path.GetFileName(path);

        WolvenKit.RED4.Archive.Archive? archive = null;
        try
        {
            EFileReadErrorCodes outcome;
            try
            {
                outcome = reader.ReadArchive(path, NoDictionaryHashService.Instance, out archive);
            }
            catch (Exception exception)
            {
                return ArchiveContents.Unreadable(
                    fileName, ArchiveFailureKind.MalformedContainer, ArchiveFailure.Evidence(exception));
            }

            // No input found so far reaches this arm - every malformed shape
            // tried throws instead - so it is written to say the same thing as
            // the arm above rather than to carry a claim of its own that
            // nothing exercises.
            if (outcome != EFileReadErrorCodes.NoError || archive is null)
            {
                return ArchiveContents.Unreadable(
                    fileName, ArchiveFailureKind.MalformedContainer, $"it reported '{outcome}'");
            }

            var entries = new List<ArchiveEntry>(archive.Files.Count);
            try
            {
                foreach (var (hash, file) in archive.Files)
                {
                    entries.Add(new ArchiveEntry(hash, _resolveName(hash), file.Size, file.ZSize));
                }
            }
            catch (Exception exception)
            {
                throw ArchiveFailure.Failure(ArchiveFailureKind.NamingFailed, fileName, exception);
            }

            return ArchiveContents.Read(fileName, entries);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    /// <summary>
    /// The path for a hash, or null when nothing available can name it.
    /// </summary>
    /// <remarks>
    /// Asked of the pinned library's own resolver rather than inferred from how
    /// an unnamed entry happens to be spelled. The library falls back to
    /// printing the hash as text, so a nameless entry and an entry named after
    /// its own hash are the same string - and telling them apart by pattern
    /// would be a guess where a definite answer exists.
    /// </remarks>
    private static string? ResolveName(ulong hash)
    {
        var resolved = ResourcePathPool.ResolveHash(hash);
        return string.IsNullOrEmpty(resolved) ? null : resolved;
    }

    private static string ResourceLibraryVersion()
    {
        var version = typeof(ArchiveReader).Assembly.GetName().Version;
        return version is null
            ? "unknown"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
