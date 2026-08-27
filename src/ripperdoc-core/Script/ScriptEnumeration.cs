namespace Ripperdoc.Core.Script;

/// <summary>
/// The script sources of one setup, in compile order, with the posture the
/// reading was taken under.
/// </summary>
public sealed class ScriptEnumeration
{
    internal ScriptEnumeration(
        IReadOnlyList<ScriptSource> sources,
        PluginScriptPosture pluginPosture,
        IReadOnlyList<string> sourcesNotSpelledInLowerCase)
    {
        Sources = sources;
        PluginPosture = pluginPosture;
        SourcesNotSpelledInLowerCase = sourcesNotSpelledInLowerCase;
    }

    /// <summary>Every source, lowest rank first.</summary>
    public IReadOnlyList<ScriptSource> Sources { get; }

    /// <summary>
    /// Whether this reading was given the scripts runtime-extension plugins
    /// contribute.
    /// </summary>
    public PluginScriptPosture PluginPosture { get; }

    /// <summary>
    /// Sources whose extension is spelled with a capital somewhere.
    /// </summary>
    /// <remarks>
    /// Reported rather than decided. This engine takes them, because the file
    /// system it read them from does not distinguish the spellings - but
    /// whether the compiler reads such a file was never observed, and on the
    /// layer the law was measured against there were none, so nothing there
    /// could have shown it either way. A caller with an empty list here has
    /// nothing at stake; a caller with a non-empty one is looking at the sources
    /// whose inclusion is this engine's choice rather than a measured rule.
    /// </remarks>
    public IReadOnlyList<string> SourcesNotSpelledInLowerCase { get; }

    /// <summary>
    /// Whether a winner drawn from this reading could be displaced by a source
    /// it never saw.
    /// </summary>
    /// <remarks>
    /// True exactly when no plugin-contributed sources were supplied. Those are
    /// appended after the whole script-directory walk and the last replacement
    /// wins, so any one of them replacing a method takes it from whichever mod
    /// this reading names.
    /// </remarks>
    public bool WinnersCanBeDisplacedByUnseenSources => PluginPosture == PluginScriptPosture.NotSupplied;
}
