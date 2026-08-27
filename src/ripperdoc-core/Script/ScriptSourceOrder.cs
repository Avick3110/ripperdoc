namespace Ripperdoc.Core.Script;

/// <summary>
/// The order script sources are compiled in, under the measured law.
/// </summary>
/// <remarks>
/// The law, measured against the shipped compiler on game 2.31 and published as
/// a finding:
/// <list type="number">
/// <item>Sources in the script directory are taken in the directory index's own
/// order - a comparison on the <strong>uppercased</strong> name - and a
/// subdirectory is walked <strong>in place</strong>, at the position its own
/// name holds among its siblings. Root files and subdirectory files
/// interleave.</item>
/// <item>Scripts contributed by runtime-extension plugins are appended after
/// that entire walk.</item>
/// </list>
/// <para>
/// The uppercasing is load-bearing and is not a stylistic choice of this
/// engine's. A comparison on lowercased names orders a leading underscore
/// before the letters instead of after them, and a plain ordinal comparison
/// orders it differently again; both were measured and both disagree with the
/// compiler. What is <em>not</em> discriminated is whether the compiler sorts
/// or simply consumes the order the file system hands it - on the single
/// case-insensitive volume measured, the two produce the same list.
/// </para>
/// <para>
/// Uppercasing here is <see cref="string.ToUpperInvariant" />, which agrees with
/// the file system's own table for ASCII. The measurement used ASCII names
/// only, so a name whose case folding is not one to one is outside what was
/// measured.
/// </para>
/// </remarks>
public static class ScriptSourceOrder
{
    private const string Extension = ".reds";

    /// <summary>
    /// Builds the compile order for a script directory.
    /// </summary>
    /// <param name="scriptDirectory">The directory the game walks.</param>
    /// <param name="pluginSources">
    /// Paths of scripts contributed by runtime-extension plugins, already in
    /// the order the plugins register them. They are appended in the order
    /// supplied and are never sorted: the order measured on a real install is
    /// not the order their names sort in, so sorting them would impose an order
    /// the compiler does not use.
    /// <para>
    /// An omitted or empty list is recorded as
    /// <see cref="PluginScriptPosture.NotSupplied" />, because nothing here can
    /// tell a caller who looked and found none from one who did not look. A
    /// caller who did look loses nothing by it: the posture costs a sentence on
    /// each result, and the alternative is an engine that takes a caller's word
    /// for the one input that outranks everything else it read.
    /// </para>
    /// </param>
    public static ScriptEnumeration Of(string scriptDirectory, IReadOnlyList<string>? pluginSources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);

        if (!Directory.Exists(scriptDirectory))
        {
            throw new DirectoryNotFoundException(
                $"There is no directory at '{scriptDirectory}'. The script layer is read from the "
                + "directory the game walks; a path that does not resolve is not an empty layer, and "
                + "reporting it as one would name no conflicts for a setup this engine never looked at.");
        }

        var walked = new List<string>();
        var oddlySpelled = new List<string>();
        Walk(scriptDirectory, relative: string.Empty, walked, oddlySpelled);

        var sources = new List<ScriptSource>(walked.Count + (pluginSources?.Count ?? 0));
        var rank = 0;
        foreach (var path in walked)
        {
            sources.Add(new ScriptSource(path, ScriptSourceOrigin.ScriptDirectory, rank++));
        }

        var posture = PluginScriptPosture.NotSupplied;
        if (pluginSources is { Count: > 0 })
        {
            posture = PluginScriptPosture.Supplied;
            foreach (var path in pluginSources)
            {
                sources.Add(new ScriptSource(path, ScriptSourceOrigin.RuntimeExtensionPlugin, rank++));
            }
        }

        return new ScriptEnumeration(sources, posture, oddlySpelled);
    }

    private static void Walk(string directory, string relative, List<string> into, List<string> oddlySpelled)
    {
        // One listing, then one ordering over it. Asking the file system for
        // files and directories separately would group them, and the measured
        // order interleaves them.
        FileSystemInfo[] entries;
        try
        {
            entries = new DirectoryInfo(directory).GetFileSystemInfos();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ScriptReadException(
                $"The script directory '{directory}' could not be listed - it raised "
                + $"{exception.GetType().Name}: {exception.Message}. No order is reported: a partial "
                + "walk would rank the sources it did reach as though the ones it did not do not "
                + "exist, and the last source to replace a method is the one that wins.",
                exception);
        }

        Array.Sort(entries, static (left, right) => string.CompareOrdinal(
            left.Name.ToUpperInvariant(), right.Name.ToUpperInvariant()));

        foreach (var entry in entries)
        {
            var childRelative = relative.Length == 0 ? entry.Name : relative + Path.DirectorySeparatorChar + entry.Name;

            if (entry is DirectoryInfo child)
            {
                Walk(child.FullName, childRelative, into, oddlySpelled);
                continue;
            }

            if (!entry.Name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!entry.Name.EndsWith(Extension, StringComparison.Ordinal))
            {
                oddlySpelled.Add(childRelative);
            }

            into.Add(childRelative);
        }
    }
}
