namespace Ripperdoc.Naming;

/// <summary>
/// Facts about the package this dictionary is read from.
/// </summary>
/// <remarks>
/// Whether a dictionary is actually loaded is deliberately not asked here. That
/// question is about the pinned library's process-wide resolver rather than
/// about this package, the engine core already depends on everything needed to
/// answer it, and keeping one home for it means this assembly's self-check and
/// an inventory's provenance cannot reach different conclusions about the same
/// process.
/// </remarks>
internal static class DictionaryPopulation
{
    /// <summary>The version of the package the dictionary is read from.</summary>
    internal static string PackageVersion()
    {
        var version = typeof(WolvenKit.Common.Services.HashService).Assembly.GetName().Version;
        return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
