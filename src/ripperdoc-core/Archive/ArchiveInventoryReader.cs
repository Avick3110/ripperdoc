using System.Reflection;
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

    /// <summary>
    /// Creates a reader that names resources with the given source.
    /// </summary>
    /// <param name="nameSource">
    /// Where names come from. <see cref="ArchiveOnlyResourceNames" /> is the
    /// posture that adds no dependency; a caller wanting the wider coverage
    /// passes the dictionary-backed source from the opt-in naming assembly.
    /// </param>
    public ArchiveInventoryReader(IResourceNameSource nameSource) =>
        _nameSource = nameSource ?? throw new ArgumentNullException(nameof(nameSource));

    /// <summary>
    /// Reads every archive in <paramref name="modDirectory" />.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory does not exist. Announced rather than treated as an empty
    /// install, because "no archives" and "nowhere to look" are different
    /// answers and only one of them is about the mods.
    /// </exception>
    /// <exception cref="ResourceNameSourceException">
    /// The naming source could not make its names available.
    /// </exception>
    public ArchiveInventory Read(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);

        if (!Directory.Exists(modDirectory))
        {
            throw new DirectoryNotFoundException(
                $"No directory at '{modDirectory}', so there is nothing to enumerate. " +
                "This is not an install with no mods - it is a path that does not resolve.");
        }

        // Before any archive is read, so that a source which cannot load fails
        // the run rather than quietly producing an under-named inventory.
        _nameSource.Prepare();

        var reader = new ArchiveReader();
        var archives = new List<ArchiveContents>();

        foreach (var path in EnumerateArchives(modDirectory))
        {
            archives.Add(ReadOne(reader, path));
        }

        return new ArchiveInventory(
            archives,
            EnumerateNestedArchives(modDirectory),
            new InventoryProvenance(modDirectory, _nameSource.Description, ResourceLibraryVersion()));
    }

    private static List<string> EnumerateArchives(string modDirectory) =>
        Directory.EnumerateFiles(modDirectory, ArchivePattern, SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

    private static List<string> EnumerateNestedArchives(string modDirectory) =>
        Directory.EnumerateDirectories(modDirectory)
            .SelectMany(directory =>
                Directory.EnumerateFiles(directory, ArchivePattern, SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(modDirectory, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static ArchiveContents ReadOne(ArchiveReader reader, string path)
    {
        var fileName = Path.GetFileName(path);

        WolvenKit.RED4.Archive.Archive? archive = null;
        try
        {
            var outcome = reader.ReadArchive(path, NoDictionaryHashService.Instance, out archive);
            if (outcome != EFileReadErrorCodes.NoError || archive is null)
            {
                return ArchiveContents.Unreadable(
                    fileName,
                    $"the pinned library reported '{outcome}' reading this archive's index");
            }

            var entries = new List<ArchiveEntry>(archive.Files.Count);
            foreach (var (hash, file) in archive.Files)
            {
                entries.Add(new ArchiveEntry(hash, ResolveName(hash), file.Size, file.ZSize));
            }

            return ArchiveContents.Read(fileName, entries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ArchiveContents.Unreadable(fileName, $"{exception.GetType().Name}: {exception.Message}");
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
