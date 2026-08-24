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
            new InventoryProvenance(
                modDirectory,
                _nameSource.Description,
                // Observed rather than inferred from the source. A dictionary
                // loads into a process-wide resolver that cannot be unloaded,
                // so a read that installed none still sees one installed
                // earlier by anything else.
                LoadedNameDictionary.IsLoaded(),
                ResourceLibraryVersion()));
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

    /// <summary>
    /// Reads one archive, or records why it could not be.
    /// </summary>
    /// <remarks>
    /// A failure here is contained to this archive. A mod directory is other
    /// people's files, and a single truncated, empty or misnamed one is an
    /// ordinary condition rather than an exceptional one - letting it end the
    /// enumeration would lose every other archive's entries to one bad
    /// download.
    /// <para>
    /// So every failure becomes a row, and the row says what happened without
    /// saying why. The underlying error is carried as evidence rather than as
    /// an explanation: the library reports a malformed container through
    /// whichever exception its own reading path happens to raise, and those
    /// exceptions name causes - a denied path, a bad argument - that are not
    /// the cause here. Repeating one as the reason would send a reader to
    /// check permissions for a file that is merely truncated.
    /// </para>
    /// </remarks>
    private static ArchiveContents ReadOne(ArchiveReader reader, string path)
    {
        var fileName = Path.GetFileName(path);

        WolvenKit.RED4.Archive.Archive? archive = null;
        try
        {
            var outcome = reader.ReadArchive(path, NoDictionaryHashService.Instance, out archive);
            if (outcome != EFileReadErrorCodes.NoError || archive is null)
            {
                return ArchiveContents.Unreadable(fileName, Unreadable($"it reported '{outcome}'"));
            }

            var entries = new List<ArchiveEntry>(archive.Files.Count);
            foreach (var (hash, file) in archive.Files)
            {
                entries.Add(new ArchiveEntry(hash, ResolveName(hash), file.Size, file.ZSize));
            }

            return ArchiveContents.Read(fileName, entries);
        }
        catch (Exception exception)
        {
            return ArchiveContents.Unreadable(
                fileName,
                Unreadable($"it raised {exception.GetType().Name}: {exception.Message}"));
        }
        finally
        {
            archive?.Dispose();
        }
    }

    /// <summary>
    /// How an archive that could not be read is described.
    /// </summary>
    /// <remarks>
    /// One sentence for both ways the read can fail, because they are the same
    /// fact to whoever is reading the report: this file is present and its
    /// index did not come back. What differs is only the evidence, which is
    /// appended rather than promoted into the claim.
    /// </remarks>
    private static string Unreadable(string evidence) =>
        $"the pinned library could not read this archive's index - {evidence.TrimEnd('.')}. "
        + "The underlying error names a cause of its own, which is evidence rather than a diagnosis; "
        + "a file that is present but unreadable here is most often truncated, still downloading, or "
        + "not an archive despite its name.";

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
