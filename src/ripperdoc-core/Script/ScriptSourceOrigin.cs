namespace Ripperdoc.Core.Script;

/// <summary>
/// Where a script source came from, which decides where it sits in the compile
/// order.
/// </summary>
/// <remarks>
/// The compile set has two sources and not one. The script directory is walked
/// first and in full; scripts contributed by runtime-extension plugins are
/// appended after that whole walk. Under the measured last-wins rule that puts
/// every plugin-contributed source above every mod's, so the distinction
/// decides winners rather than merely describing provenance.
/// </remarks>
public enum ScriptSourceOrigin
{
    /// <summary>A source found by walking the script directory.</summary>
    ScriptDirectory,

    /// <summary>
    /// A source contributed by a runtime-extension plugin, appended after the
    /// script directory's walk.
    /// </summary>
    /// <remarks>
    /// Their order among themselves is the order the plugins register them,
    /// which is not the order their names would sort in - so a caller supplies
    /// them already ordered rather than asking this engine to sort them.
    /// </remarks>
    RuntimeExtensionPlugin,
}
