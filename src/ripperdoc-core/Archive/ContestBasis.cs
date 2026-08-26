namespace Ripperdoc.Core.Archive;

/// <summary>
/// What a contest was computed over.
/// </summary>
/// <remarks>
/// It has one member because one basis is reachable. An archive's index
/// carries a resource's path hash, its sizes and its segment offsets - it does
/// not carry what is inside the resource, and a contest between two resources
/// at different paths lives inside them. Naming a basis this project cannot
/// compute would put a member on this enum that nothing ever returns, which
/// says a capability exists where none does.
/// </remarks>
public enum ContestBasis
{
    /// <summary>
    /// Two archives carry the same resource path.
    /// </summary>
    /// <remarks>
    /// Computed from indices alone, so it costs one index read per archive and
    /// sees every resource an archive carries. What it cannot see is a contest
    /// between archives that share no path - the framework layer reports one
    /// such class at runtime, where two mods supply different localisation
    /// resources that declare the same entry. Those are invisible to this
    /// basis by construction, not by omission.
    /// </remarks>
    ResourcePath,
}
