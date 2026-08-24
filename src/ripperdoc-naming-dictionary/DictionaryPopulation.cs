using System.Reflection;
using Ripperdoc.Core.Archive;
using WolvenKit.Core.Helpers;
using WolvenKit.RED4.Types.Pools;

namespace Ripperdoc.Naming;

/// <summary>
/// Whether the pinned library's resource resolver actually holds a dictionary.
/// </summary>
/// <remarks>
/// There is no public way to ask. The resolver takes a lookup table and exposes
/// only per-hash questions, so the population it holds can be observed only
/// through the field it keeps it in. That is a real fragility and it is
/// deliberate: the alternative is to confirm the load by asking for a
/// particular known resource path, which would mean carrying a real entry
/// identifier in this repository, and the boundary this project draws puts that
/// on the wrong side.
/// <para>
/// Reflection that no longer finds what it expects throws rather than answering,
/// so a change in the pinned library surfaces as a named failure of the
/// self-check instead of as a silently unverified load.
/// </para>
/// </remarks>
internal static class DictionaryPopulation
{
    private const string PoolField = "s_pool";
    private const string NativeField = "_nativePool";

    /// <summary>
    /// Whether the resolver's dictionary holds any name at all.
    /// </summary>
    /// <remarks>
    /// Asked as a yes-or-no rather than as a count: the question is whether the
    /// load happened, a full count of a loaded dictionary runs to millions of
    /// entries, and walking all of them to learn something the first entry
    /// already settles would be a cost paid for nothing.
    /// </remarks>
    /// <exception cref="ResourceNameSourceException">
    /// The resolver's internals are not shaped the way this check expects.
    /// </exception>
    internal static bool AnyNames()
    {
        var poolField = typeof(ResourcePathPool)
            .GetField(PoolField, BindingFlags.NonPublic | BindingFlags.Static);
        var pool = poolField?.GetValue(null)
            ?? throw Unexpected($"{nameof(ResourcePathPool)} has no static '{PoolField}' to read");

        var nativeField = pool.GetType()
            .GetField(NativeField, BindingFlags.NonPublic | BindingFlags.Instance);
        var native = nativeField?.GetValue(pool);

        if (native is null)
        {
            throw Unexpected($"the resolver's '{NativeField}' is absent or null");
        }

        if (native is not LookupTable table)
        {
            throw Unexpected(
                $"the resolver's '{NativeField}' is a {native.GetType().Name}, not a {nameof(LookupTable)}");
        }

        foreach (var _ in table)
        {
            return true;
        }

        return false;
    }

    /// <summary>The version of the package the dictionary is read from.</summary>
    internal static string PackageVersion()
    {
        var version = typeof(WolvenKit.Common.Services.HashService).Assembly.GetName().Version;
        return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static ResourceNameSourceException Unexpected(string what) =>
        new($"The dictionary load cannot be confirmed because {what}. This check reads the pinned " +
            "library's internals deliberately, so a change there stops the run rather than letting " +
            "an unverified load pass as a verified one. Confirm the pinned WolvenKit version.");
}
