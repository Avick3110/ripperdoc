using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The central pin file, located from the test binary.
/// </summary>
/// <remarks>
/// Read as a file rather than inferred from loaded assemblies, because the
/// pinned family is deliberately not all loaded into one process. A check that
/// asked the runtime what it had loaded would be silent about exactly the
/// package this project went out of its way to keep out of the engine core.
/// </remarks>
internal static class PinnedPackages
{
    internal const string FileName = "Directory.Packages.props";

    /// <summary>
    /// Where the pin file is, or a failed check saying it could not be found.
    /// </summary>
    internal static string FilePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        Assert.Fail(
            $"This check reads the central pin file and could not find '{FileName}' above "
            + $"'{AppContext.BaseDirectory}'. It fails rather than skipping, because a pin nothing is "
            + "holding to a version is what this exists to prevent.");
        return string.Empty;
    }
}
