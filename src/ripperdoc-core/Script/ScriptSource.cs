namespace Ripperdoc.Core.Script;

/// <summary>
/// One script source in the compile order, at the rank it holds.
/// </summary>
/// <param name="Path">
/// How this source is named. For a walked source this is its path relative to
/// the script directory; for a plugin-contributed one it is whatever the caller
/// supplied.
/// </param>
/// <param name="Origin">Which of the two sources of the compile set it came from.</param>
/// <param name="Rank">
/// Its position in the compile order, lowest first. Ranks are unique: unlike
/// the archive layer, nothing here is left tied, because the order was measured
/// directly from the compiler's own printed list rather than inferred.
/// </param>
public sealed record ScriptSource(string Path, ScriptSourceOrigin Origin, int Rank)
{
    /// <summary>
    /// How to name this source to a reader.
    /// </summary>
    /// <remarks>
    /// A plugin-contributed source is marked as one. Two sources can otherwise
    /// carry the same-looking path while sitting on opposite sides of the whole
    /// script directory, and which of the two a reader is looking at is the
    /// thing that decides a contest.
    /// </remarks>
    public string Display => Origin == ScriptSourceOrigin.RuntimeExtensionPlugin
        ? $"{Path} (runtime-extension plugin)"
        : Path;
}
