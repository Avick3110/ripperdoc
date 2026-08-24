using WolvenKit.Common;
using WolvenKit.Common.Services;
using WolvenKit.Core.Interfaces;
using WolvenKit.RED4.Archive.IO;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Archives authored by this project, for checks that need a real container
/// rather than a stand-in.
/// </summary>
/// <remarks>
/// Every byte is this project's own: the entries are invented paths carrying
/// invented content, written through the pinned library's own writer. Nothing
/// game-derived and nothing of anyone else's is involved, which is what lets
/// these run on a bare runner.
/// <para>
/// The writer packs only the extensions the format knows, and silently packs
/// nothing at all when given anything else - it still reports success. So
/// <see cref="Write" /> verifies that what it asked for is what landed, rather
/// than handing back an empty archive that would make a check pass by having
/// nothing in it to disagree with.
/// </para>
/// </remarks>
internal static class SyntheticArchive
{
    /// <summary>An extension the archive format recognises.</summary>
    internal const string PackableExtension = ".json";

    /// <summary>
    /// Writes an archive containing one entry per given relative path.
    /// </summary>
    /// <returns>The path of the archive written.</returns>
    internal static string Write(string directory, string archiveName, params string[] relativePaths)
    {
        if (relativePaths.Length == 0)
        {
            throw new ArgumentException("An archive with no entries proves nothing.", nameof(relativePaths));
        }

        var staging = Path.Combine(directory, "staging-" + Path.GetFileNameWithoutExtension(archiveName));
        Directory.CreateDirectory(staging);

        foreach (var relativePath in relativePaths)
        {
            var full = Path.Combine(staging, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, $"{{\"ripperdoc-synthetic\":\"{Path.GetFileNameWithoutExtension(relativePath)}\"}}");
        }

        var archivePath = Path.Combine(directory, archiveName);
        var writer = new ArchiveWriter(new SilentHashService(), new SilentLogger());

        using (var stream = File.Create(archivePath))
        {
            if (!writer.WriteArchive(new DirectoryInfo(staging), stream))
            {
                throw new InvalidOperationException($"The pinned library declined to write '{archiveName}'.");
            }
        }

        Directory.Delete(staging, recursive: true);

        var reader = new ArchiveReader();
        if (reader.ReadArchive(archivePath, new SilentHashService(), out var written) != WolvenKit.RED4.Archive.IO.EFileReadErrorCodes.NoError
            || written is null)
        {
            throw new InvalidOperationException($"'{archiveName}' was written but cannot be read back.");
        }

        var packed = written.Files.Count;
        written.Dispose();

        if (packed != relativePaths.Length)
        {
            throw new InvalidOperationException(
                $"'{archiveName}' was asked for {relativePaths.Length} entries and holds {packed}. " +
                $"The writer packs only known extensions - use {PackableExtension}.");
        }

        return archivePath;
    }

    private sealed class SilentHashService : IHashService
    {
        public Task Loaded { get; } = Task.CompletedTask;
        public void Load() { }
        public bool Contains(ulong key, bool checkUserHashes) => false;
        public string Get(ulong key) => null!;
        public IEnumerable<ulong> GetAllHashes() => [];
        public IEnumerable<ulong> GetMissingHashes() => [];
        public string GetGuessedExtension(ulong key) => null!;
    }

    private sealed class SilentLogger : ILoggerService
    {
        public LoggerVerbosity LoggerVerbosity { get; set; }
        public void SetLoggerVerbosity(LoggerVerbosity value) { }
        public void Info(string value) { }
        public void Info(int id, string value) { }
        public void Warning(string value) { }
        public void Warning(int id, string value) { }
        public void Error(string value) { }
        public void Error(int id, string value) { }
        public void Success(string value) { }
        public void Success(int id, string value) { }
        public void Debug(string value) { }
        public void Debug(int id, string value) { }
        public void Error(Exception ex) { }
        public void Error(int id, Exception ex) { }
    }
}
