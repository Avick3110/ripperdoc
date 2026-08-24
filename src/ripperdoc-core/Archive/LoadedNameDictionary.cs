using System.Reflection;
using WolvenKit.Core.Helpers;
using WolvenKit.RED4.Types.Pools;

namespace Ripperdoc.Core.Archive;

/// <summary>
/// Whether a resource-name dictionary is loaded in this process.
/// </summary>
/// <remarks>
/// The pinned library resolves resource paths through a resolver that is
/// process-wide, additive and cannot be unloaded. So a dictionary is not a
/// property of one reader or one inventory: once anything in the process
/// installs one, every later read sees it, whether or not that read asked for
/// it.
/// <para>
/// That makes what a naming source <em>intends</em> the wrong thing to write
/// into an artifact's provenance. A source that installs no dictionary cannot
/// truthfully say none is installed, because something else may already have
/// done so. What can be said truthfully is what was observed at the moment the
/// read happened, and this is what observes it.
/// </para>
/// <para>
/// It lives in the engine core, and needs nothing the core did not already
/// depend on - the resolver and its table both come from packages already
/// referenced. Observing that a dictionary is present is not the same as
/// carrying one, and only the second would have moved the dependency boundary.
/// </para>
/// <para>
/// The observation reads the pinned library's internals, which is fragile on
/// purpose: the resolver takes a table and exposes only per-hash questions, so
/// the alternative is to ask after one known resource path by name and carry a
/// real entry identifier in this repository. Reflection that no longer finds
/// what it expects throws rather than answering, so a change in the pinned
/// library stops the run by name instead of quietly reporting "no dictionary"
/// forever after.
/// </para>
/// </remarks>
public static class LoadedNameDictionary
{
    private const string PoolField = "s_pool";
    private const string NativeField = "_nativePool";

    /// <summary>
    /// Whether the resolver currently holds a dictionary of known resource
    /// paths.
    /// </summary>
    /// <remarks>
    /// <strong>What this does and does not establish.</strong> It establishes
    /// that a dictionary was <em>loaded</em> - the resolver holds at least one
    /// name. It does <em>not</em> establish that naming a resource through it
    /// works end to end; that is a different claim and it is carried by the
    /// archive-lane tier's cross-posture comparison, which reads one real
    /// directory under both postures and holds them to disagreeing about names
    /// while agreeing about contents.
    /// <para>
    /// Asked as a yes-or-no rather than as a count: the question is whether a
    /// dictionary is there, a loaded one runs to millions of entries, and
    /// walking all of them to learn what the first entry settles would be a
    /// cost paid for nothing.
    /// </para>
    /// </remarks>
    /// <exception cref="ResourceNameSourceException">
    /// The resolver's internals are not shaped the way this observation
    /// expects.
    /// </exception>
    public static bool IsLoaded() => IsLoaded(PoolField, NativeField);

    /// <summary>
    /// The same observation, against named fields.
    /// </summary>
    /// <remarks>
    /// Internal so that a check can drive it at a field this library does not
    /// have, and hold the refusal to its message. The refusal tells a reader to
    /// confirm the pinned version, and a sentence that directs an action is
    /// worth more than an assertion that it exists.
    /// </remarks>
    internal static bool IsLoaded(string poolFieldName, string nativeFieldName)
    {
        var poolField = typeof(ResourcePathPool)
            .GetField(poolFieldName, BindingFlags.NonPublic | BindingFlags.Static);
        var pool = poolField?.GetValue(null)
            ?? throw Unexpected($"{nameof(ResourcePathPool)} has no static '{poolFieldName}' to read");

        var nativeField = pool.GetType()
            .GetField(nativeFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        var native = nativeField?.GetValue(pool);

        if (native is null)
        {
            throw Unexpected($"the resolver's '{nativeFieldName}' is absent or null");
        }

        if (native is not LookupTable table)
        {
            throw Unexpected(
                $"the resolver's '{nativeFieldName}' is a {native.GetType().Name}, not a {nameof(LookupTable)}");
        }

        foreach (var _ in table)
        {
            return true;
        }

        return false;
    }

    private static ResourceNameSourceException Unexpected(string what) =>
        new($"Whether a name dictionary is loaded cannot be established because {what}. This reads the "
            + "pinned library's internals deliberately, so a change there stops the run rather than "
            + "letting an unverified answer pass as a verified one. Confirm the pinned WolvenKit version.");
}
