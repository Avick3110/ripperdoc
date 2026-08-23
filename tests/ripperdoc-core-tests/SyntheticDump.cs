using System.Text;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// A dump of generated type information, written to a temporary directory for
/// one check and removed afterwards.
/// </summary>
/// <remarks>
/// Every byte here is authored by this project. The names are invented and
/// resemble the game's only in shape, because the shape is the whole of what
/// the reader under test cares about - nothing of the game's own type
/// information is in this repository or reachable from a bare runner.
/// </remarks>
public sealed class SyntheticDump : IDisposable
{
    private SyntheticDump(string root)
    {
        Root = root;
        JsonDirectory = Path.Combine(root, "json");
    }

    /// <summary>The temporary directory holding the dump.</summary>
    public string Root { get; }

    /// <summary>The path to hand a reader - the dump's json output.</summary>
    public string JsonDirectory { get; }

    /// <summary>
    /// Build a dump carrying the given class and enumeration documents.
    /// </summary>
    /// <param name="classes">One JSON document per class, in writing order.</param>
    /// <param name="enums">One JSON document per enumeration.</param>
    /// <param name="bitfields">One JSON document per bitfield.</param>
    /// <returns>The dump, which removes itself when disposed.</returns>
    public static SyntheticDump Of(
        IEnumerable<string>? classes = null,
        IEnumerable<string>? enums = null,
        IEnumerable<string>? bitfields = null)
    {
        var dump = new SyntheticDump(Path.Combine(
            Path.GetTempPath(),
            "ripperdoc-synthetic-dump-" + Guid.NewGuid().ToString("n")));

        Write(Path.Combine(dump.JsonDirectory, "classes"), classes);
        Write(Path.Combine(dump.JsonDirectory, "enums"), enums);
        Write(Path.Combine(dump.JsonDirectory, "bitfields"), bitfields);

        return dump;
    }

    /// <summary>
    /// Remove one of the three directories a dump must have, to exercise a
    /// reader meeting an incomplete one.
    /// </summary>
    /// <param name="name">The directory to remove.</param>
    public void Remove(string name) => Directory.Delete(Path.Combine(JsonDirectory, name), recursive: true);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static void Write(string directory, IEnumerable<string>? documents)
    {
        Directory.CreateDirectory(directory);

        var index = 0;
        foreach (var document in documents ?? [])
        {
            // Named by position rather than by the name inside the document.
            // The reader keys on what the document says, so a file name it does
            // not read is the right thing for a fixture to be careless about -
            // and a check that a document and its file name disagree is then a
            // check of the reader rather than of this helper.
            File.WriteAllText(
                Path.Combine(directory, $"document-{index++:D4}.json"),
                document,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
