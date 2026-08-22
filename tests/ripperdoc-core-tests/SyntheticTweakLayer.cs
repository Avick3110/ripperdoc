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
    private SyntheticTweakLayer(string root) => Root = root;

    /// <summary>The layer's directory.</summary>
    internal string Root { get; }

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

        var layer = new SyntheticTweakLayer(root);

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

    /// <summary>Enumerate this layer.</summary>
    /// <returns>The layer in read order.</returns>
    internal TweakLayer Enumerate() => TweakLayer.Enumerate(Root);

    /// <summary>Enumerate and replay this layer.</summary>
    /// <returns>The resolved state.</returns>
    internal TweakResolvedState Replay(
        TweakInheritanceMap? inheritance = null,
        ITweakValueSource? values = null)
    {
        var layer = Enumerate();

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
