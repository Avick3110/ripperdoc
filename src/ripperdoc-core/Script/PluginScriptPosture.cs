namespace Ripperdoc.Core.Script;

/// <summary>
/// Whether a reading of the script layer was given the scripts that
/// runtime-extension plugins contribute.
/// </summary>
/// <remarks>
/// This is carried rather than assumed because the answer changes which mod a
/// contest names. Plugin-contributed sources are appended after the whole
/// script-directory walk, and the last replacement of a method wins, so a
/// plugin source replacing a method beats every mod that replaced it. A reading
/// taken without them can be right and cannot be known to be right, and saying
/// which of the two it is belongs beside every winner it reports.
/// </remarks>
public enum PluginScriptPosture
{
    /// <summary>
    /// The reading was given no plugin-contributed sources, and does not know
    /// whether any exist.
    /// </summary>
    /// <remarks>
    /// The default, because the script directory is what a caller can find
    /// unaided. Every winner reported under this posture is provisional.
    /// </remarks>
    NotSupplied,

    /// <summary>
    /// The caller supplied the plugin-contributed sources, in the order the
    /// plugins register them.
    /// </summary>
    Supplied,
}
