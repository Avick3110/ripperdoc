using Ripperdoc.Core.Tweak;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// A tweak layer built for a check, on disk, and removed afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Every byte here is written by the check that uses it. Nothing is copied from
/// a game install or from anyone's mod - the ordering rules under test are about
/// names and directory structure, and names are free to invent.
/// </para>
/// <para>
/// The files are real files in a real directory because the thing being checked
/// is a directory walk. A walk tested against an in-memory list of paths would
/// be a check of the list.
/// </para>
/// </remarks>
internal sealed class SyntheticTweakLayer : IDisposable
{
    private SyntheticTweakLayer(string root, IReadOnlyList<string> declared)
    {
        Root = root;
        Declared = declared;
    }

    /// <summary>The layer's directory.</summary>
    internal string Root { get; }

    /// <summary>
    /// The paths this layer was built from, in the order they were given.
    /// </summary>
    internal IReadOnlyList<string> Declared { get; }

    /// <summary>
    /// Build a layer from paths and contents.
    /// </summary>
    /// <param name="files">
    /// Relative paths within the layer, each with the text to write into it.
    /// </param>
    /// <returns>The layer.</returns>
    internal static SyntheticTweakLayer Of(params (string Path, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "tweak-layer-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(root);

        var layer = new SyntheticTweakLayer(root, files.Select(file => file.Path).ToArray());

        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, path.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return layer;
    }

    /// <summary>Build a layer of empty files.</summary>
    /// <param name="paths">Relative paths within the layer.</param>
    /// <returns>The layer.</returns>
    internal static SyntheticTweakLayer OfEmpty(params string[] paths) =>
        Of(paths.Select(path => (path, string.Empty)).ToArray());

    /// <summary>
    /// Enumerate this layer by walking its directory.
    /// </summary>
    /// <returns>The layer in read order.</returns>
    /// <remarks>
    /// For checks about the walk itself. The order a directory hands back is
    /// the volume's business and differs between filesystems, so a check that
    /// asserts a particular order after calling this is asserting a property of
    /// the machine it runs on. Everything downstream of the walk uses
    /// <see cref="EnumerateAsDeclared"/> instead.
    /// </remarks>
    internal TweakLayer Enumerate() => TweakLayer.Enumerate(Root);

    /// <summary>
    /// Build this layer from the order its files were declared in.
    /// </summary>
    /// <returns>The layer in read order.</returns>
    /// <remarks>
    /// The files are real and are really read; what is supplied is the walk
    /// order, so a check downstream of the walk states the order it means
    /// instead of inheriting one from whichever filesystem it runs on. Grouping
    /// still applies on top, because that is the framework's rule and not the
    /// volume's.
    /// </remarks>
    internal TweakLayer EnumerateAsDeclared() =>
        TweakLayer.Of(Declared, TweakLayer.IsCollated(Declared));

    /// <summary>Enumerate and replay this layer.</summary>
    /// <returns>The resolved state.</returns>
    internal TweakResolvedState Replay(
        TweakInheritanceMap? inheritance = null,
        ITweakValueSource? values = null)
    {
        var layer = EnumerateAsDeclared();

        return TweakResolvedState.Replay(
            layer,
            TweakFileReader.ReadLayer(layer, Root),
            inheritance ?? TweakInheritanceMap.None,
            values);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A check's temporary directory outliving the check is untidy and
            // nothing more; failing the run over it would report a defect in
            // the engine that is not there.
        }
    }
}
