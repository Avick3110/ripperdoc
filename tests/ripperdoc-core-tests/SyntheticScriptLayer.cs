namespace Ripperdoc.Core.Tests;

/// <summary>
/// A script layer built for a check, on disk, and removed afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Every byte here is authored by the check that uses it. Nothing is copied
/// from a game install or from anyone's mod: the rules under test are about
/// names, directory structure and annotation text, and all three are free to
/// invent.
/// </para>
/// <para>
/// Real files in a real directory, because what is being checked is a directory
/// walk. A walk checked against an in-memory list of paths would be a check of
/// the list.
/// </para>
/// </remarks>
internal sealed class SyntheticScriptLayer : IDisposable
{
    private SyntheticScriptLayer(string root) => Root = root;

    /// <summary>The layer's directory.</summary>
    internal string Root { get; }

    /// <summary>
    /// Builds a layer from relative paths and the text to write into each.
    /// </summary>
    internal static SyntheticScriptLayer Of(params (string Path, string Text)[] files)
    {
        var root = Directory.CreateTempSubdirectory("ripperdoc-scripts-").FullName;

        foreach (var (path, text) in files)
        {
            var full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        return new SyntheticScriptLayer(root);
    }

    /// <summary>A source that replaces one method, returning nothing of note.</summary>
    internal static string Replaces(string type, string method) =>
        $"@replaceMethod({type})\npublic func {method}() -> String {{\n  return \"x\";\n}}\n";

    /// <summary>A source that wraps one method and calls what it wraps.</summary>
    internal static string Wraps(string type, string method) =>
        $"@wrapMethod({type})\npublic func {method}() -> String {{\n  return \"x\" + wrappedMethod();\n}}\n";

    /// <summary>A source that wraps one method and never calls what it wraps.</summary>
    internal static string WrapsWithoutCalling(string type, string method) =>
        $"@wrapMethod({type})\npublic func {method}() -> String {{\n  return \"x\";\n}}\n";

    /// <summary>A wrap whose body never closes, so its end cannot be found.</summary>
    internal static string WrapWithAnUnclosedBody(string type, string method) =>
        $"@wrapMethod({type})\npublic func {method}() -> String {{\n  return \"x\" + wrappedMethod();\n";

    /// <summary>
    /// The conditional-compilation gate, as it appears above a declaration.
    /// </summary>
    /// <remarks>
    /// The condition's text is deliberately arbitrary. This engine reads that a
    /// gate is there and never what it evaluates to, so a fixture that picked a
    /// condition meant to be true or false would be asserting the thing the
    /// engine refuses to decide.
    /// </remarks>
    internal static string Gate => "@if(ModuleExists(\"SomeOtherMod\"))\n";

    /// <summary>A gated replacement of one method.</summary>
    internal static string GatedReplaces(string type, string method) =>
        Gate + Replaces(type, method);

    /// <summary>A gated wrap of one method.</summary>
    internal static string GatedWraps(string type, string method) =>
        Gate + Wraps(type, method);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A check's temp tree failing to come off is not a result. Leaving
            // it is untidy; failing the check for it would report a defect in
            // the engine that is not there.
        }
    }
}
